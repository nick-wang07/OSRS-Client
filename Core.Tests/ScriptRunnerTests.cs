using Core.Safety;
using Core.Scripting;
using Core.Tests.Fakes;
using Core.Windows;

namespace Core.Tests;

public class ScriptRunnerTests
{
    /// <summary>Script whose body is supplied by the test.</summary>
    private sealed class DelegateScript(Func<CancellationToken, Task> body, string name = "test") : IScript
    {
        public string Name => name;

        public string Description => "test script";

        public Task Run(ScriptContext context, CancellationToken cancellationToken) => body(cancellationToken);
    }

    private static IScript Completes() => new DelegateScript(_ => Task.CompletedTask);

    private static IScript Throws(Exception exception) => new DelegateScript(_ => Task.FromException(exception));

    private static IScript RunsUntilCanceled(TaskCompletionSource started) =>
        new DelegateScript(async token =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
        });

    /// <summary>A context wired entirely from fakes; the runner passes it straight through.</summary>
    private static ScriptContext Context()
    {
        var native = new FakeNativeApi();
        var finder = new FakeWindowFinder();

        return new ScriptContext(
            new GameWindowProvider(native, finder),
            new FakeInputProvider(),
            new FakeScreenCapture(),
            new RecordingLogger());
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(5);
        }
    }

    [Fact]
    public void StartsIdle()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());

        Assert.Equal(ScriptState.Idle, runner.State);
        Assert.Null(runner.Current);
        Assert.Null(runner.Fault);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public void Start_RejectsNullScript()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());

        Assert.Throws<ArgumentNullException>(() => runner.Start(null!, Context()));
    }

    [Fact]
    public async Task Start_RunsScriptToCompletion()
    {
        using var killSwitch = new KillSwitch();
        var logger = new RecordingLogger();
        using var runner = new ScriptRunner(killSwitch, logger);

        runner.Start(Completes(), Context());
        await runner.Stop();

        Assert.Equal(ScriptState.Completed, runner.State);
        Assert.Null(runner.Fault);
        Assert.Contains(logger.Infos, message => message.Contains("completed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Start_RecordsCurrentScript()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        IScript script = Completes();

        runner.Start(script, Context());

        Assert.Same(script, runner.Current);

        await runner.Stop();
    }

    [Fact]
    public async Task Start_CapturesFaultWithoutRethrowing()
    {
        using var killSwitch = new KillSwitch();
        var logger = new RecordingLogger();
        using var runner = new ScriptRunner(killSwitch, logger);
        var boom = new InvalidOperationException("tree not found");

        runner.Start(Throws(boom), Context());
        await runner.Stop();

        Assert.Equal(ScriptState.Faulted, runner.State);
        Assert.Same(boom, runner.Fault);
        Assert.Same(boom, Assert.Single(logger.Errors).Exception);
    }

    [Fact]
    public async Task Start_RejectsSecondScriptWhileRunning()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        var started = new TaskCompletionSource();

        runner.Start(RunsUntilCanceled(started), Context());
        await started.Task;

        // Concurrent scripts would interleave mouse events into nonsense.
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => runner.Start(Completes(), Context()));

        Assert.Contains("already running", error.Message, StringComparison.Ordinal);

        await runner.Stop();
    }

    [Fact]
    public async Task Start_AllowsAnotherScriptAfterThePreviousFinishes()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());

        runner.Start(Completes(), Context());
        await runner.Stop();

        runner.Start(Completes(), Context());
        await runner.Stop();

        Assert.Equal(ScriptState.Completed, runner.State);
    }

    [Fact]
    public void Start_RefusesWhenKillSwitchAlreadyTriggered()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        killSwitch.Trigger("armed");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => runner.Start(Completes(), Context()));

        Assert.Contains("kill switch", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        using var killSwitch = new KillSwitch();
        var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        runner.Dispose();

        Assert.Throws<ObjectDisposedException>(() => runner.Start(Completes(), Context()));
    }

    [Fact]
    public async Task StopAsync_CancelsARunningScript()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        var started = new TaskCompletionSource();

        runner.Start(RunsUntilCanceled(started), Context());
        await started.Task;

        await runner.Stop();

        Assert.Equal(ScriptState.Stopped, runner.State);
        Assert.Null(runner.Fault);
    }

    [Fact]
    public async Task StopAsync_IsSafeWhenNothingIsRunning()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());

        await runner.Stop();

        Assert.Equal(ScriptState.Idle, runner.State);
    }

    [Fact]
    public async Task KillSwitch_StopsTheRunningScript()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        var started = new TaskCompletionSource();

        runner.Start(RunsUntilCanceled(started), Context());
        await started.Task;

        // The whole point of the kill switch: pressing it must stop work already in flight.
        killSwitch.Trigger("F12 pressed");

        await WaitFor(() => runner.State == ScriptState.Stopped);

        Assert.Equal(ScriptState.Stopped, runner.State);
    }

    [Fact]
    public async Task StateChanged_ReportsTransitions()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());
        List<ScriptState> states = [];
        runner.StateChanged += (_, state) => states.Add(state);

        runner.Start(Completes(), Context());
        await runner.Stop();

        Assert.Equal([ScriptState.Running, ScriptState.Completed], states);
    }

    [Fact]
    public async Task Fault_IsClearedOnNextRun()
    {
        using var killSwitch = new KillSwitch();
        using var runner = new ScriptRunner(killSwitch, new RecordingLogger());

        runner.Start(Throws(new InvalidOperationException("boom")), Context());
        await runner.Stop();
        Assert.NotNull(runner.Fault);

        runner.Start(Completes(), Context());
        await runner.Stop();

        Assert.Null(runner.Fault);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        using var killSwitch = new KillSwitch();
        var runner = new ScriptRunner(killSwitch, new RecordingLogger());

        runner.Dispose();
        runner.Dispose();
    }
}
