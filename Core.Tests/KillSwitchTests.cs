using Core.Native;
using Core.Safety;
using Core.Tests.Fakes;

namespace Core.Tests;

public class KillSwitchTests
{
    [Fact]
    public void StartsUntriggeredWithLiveToken()
    {
        using var killSwitch = new KillSwitch();

        Assert.False(killSwitch.IsTriggered);
        Assert.False(killSwitch.Token.IsCancellationRequested);
        Assert.Null(killSwitch.Reason);
    }

    [Fact]
    public void Trigger_CancelsTheToken()
    {
        using var killSwitch = new KillSwitch();

        killSwitch.Trigger("F12 pressed");

        Assert.True(killSwitch.IsTriggered);
        Assert.True(killSwitch.Token.IsCancellationRequested);
        Assert.Equal("F12 pressed", killSwitch.Reason);
    }

    [Fact]
    public void Trigger_RaisesEventWithReason()
    {
        using var killSwitch = new KillSwitch();
        List<string> reasons = [];
        killSwitch.Triggered += (_, reason) => reasons.Add(reason);

        killSwitch.Trigger("panic");

        Assert.Equal("panic", Assert.Single(reasons));
    }

    [Fact]
    public void Trigger_TokenIsAlreadyCanceledWhenHandlersRun()
    {
        using var killSwitch = new KillSwitch();
        bool canceledDuringHandler = false;
        killSwitch.Triggered += (_, _) => canceledDuringHandler = killSwitch.Token.IsCancellationRequested;

        killSwitch.Trigger("stop");

        // Handlers commonly inspect the token; it must already be canceled by then.
        Assert.True(canceledDuringHandler);
    }

    [Fact]
    public void Trigger_IsIdempotent()
    {
        using var killSwitch = new KillSwitch();
        int raised = 0;
        killSwitch.Triggered += (_, _) => raised++;

        killSwitch.Trigger("first");
        killSwitch.Trigger("second");
        killSwitch.Trigger("third");

        Assert.Equal(1, raised);
        Assert.Equal("first", killSwitch.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Trigger_RejectsBlankReason(string? reason)
    {
        using var killSwitch = new KillSwitch();

        Assert.ThrowsAny<ArgumentException>(() => killSwitch.Trigger(reason!));
        Assert.False(killSwitch.IsTriggered);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var killSwitch = new KillSwitch();

        killSwitch.Dispose();
        killSwitch.Dispose();
    }

    [Fact]
    public void Trigger_AfterDispose_DoesNotThrow()
    {
        var killSwitch = new KillSwitch();
        killSwitch.Dispose();

        killSwitch.Trigger("late");

        Assert.False(killSwitch.IsTriggered);
    }
}

public class HotkeyKillSwitchMonitorTests
{
    private static (HotkeyKillSwitchMonitor Monitor, FakeNativeApi Native, KillSwitch Switch) Build(
        VirtualKey hotkey = VirtualKey.F12)
    {
        var native = new FakeNativeApi();
        var killSwitch = new KillSwitch();

        var monitor = new HotkeyKillSwitchMonitor(native, killSwitch, hotkey)
        {
            PollInterval = TimeSpan.FromMilliseconds(1),
        };

        return (monitor, native, killSwitch);
    }

    [Fact]
    public void Constructor_RejectsNullNativeApi() =>
        Assert.Throws<ArgumentNullException>(() => new HotkeyKillSwitchMonitor(null!, new KillSwitch()));

    [Fact]
    public void Constructor_RejectsNullKillSwitch() =>
        Assert.Throws<ArgumentNullException>(() => new HotkeyKillSwitchMonitor(new FakeNativeApi(), null!));

    [Fact]
    public void Hotkey_DefaultsToF12()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            Assert.Equal(VirtualKey.F12, monitor.Hotkey);
        }
    }

    [Fact]
    public void Poll_DoesNothingWhenHotkeyIsUp()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            Assert.False(monitor.Poll());
            Assert.False(killSwitch.IsTriggered);
        }
    }

    [Fact]
    public void Poll_TriggersWhenHotkeyIsDown()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            native.KeysDown.Add(VirtualKey.F12);

            Assert.True(monitor.Poll());
            Assert.True(killSwitch.IsTriggered);
            Assert.Contains("F12", killSwitch.Reason!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Poll_IgnoresOtherKeys()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            native.KeysDown.Add(VirtualKey.F1);

            Assert.False(monitor.Poll());
            Assert.False(killSwitch.IsTriggered);
        }
    }

    [Fact]
    public void Poll_RespectsCustomHotkey()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build(VirtualKey.Escape);
        using (monitor)
        using (killSwitch)
        {
            native.KeysDown.Add(VirtualKey.Escape);

            Assert.True(monitor.Poll());
        }
    }

    [Fact]
    public void Poll_ReturnsFalseOnceAlreadyTriggered()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            native.KeysDown.Add(VirtualKey.F12);

            Assert.True(monitor.Poll());
            Assert.False(monitor.Poll());
        }
    }

    [Fact]
    public async Task Start_TriggersWhenHotkeyPressedLater()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            monitor.Start();
            Assert.True(monitor.IsRunning);

            native.KeysDown.Add(VirtualKey.F12);

            await WaitFor(() => killSwitch.IsTriggered);

            Assert.True(killSwitch.IsTriggered);
        }
    }

    [Fact]
    public void Start_IsIdempotent()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            monitor.Start();
            monitor.Start();

            Assert.True(monitor.IsRunning);
        }
    }

    [Fact]
    public void Start_AfterDispose_Throws()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (killSwitch)
        {
            monitor.Dispose();

            Assert.Throws<ObjectDisposedException>(monitor.Start);
        }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (killSwitch)
        {
            monitor.Start();
            monitor.Dispose();
            monitor.Dispose();
        }
    }

    [Fact]
    public void IsRunning_IsFalseBeforeStart()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            Assert.False(monitor.IsRunning);
        }
    }

    [Fact]
    public async Task Start_LoopEndsOnceTheHotkeyFires()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            monitor.Start();
            native.KeysDown.Add(VirtualKey.F12);

            await WaitFor(() => !monitor.IsRunning);

            // The loop should retire itself rather than spinning after triggering.
            Assert.False(monitor.IsRunning);
        }
    }

    [Fact]
    public async Task Dispose_StopsTheLoopWithoutTriggering()
    {
        (HotkeyKillSwitchMonitor monitor, _, KillSwitch killSwitch) = Build();
        using (killSwitch)
        {
            monitor.Start();
            Assert.True(monitor.IsRunning);

            monitor.Dispose();

            await WaitFor(() => !monitor.IsRunning);

            Assert.False(monitor.IsRunning);
            Assert.False(killSwitch.IsTriggered);
        }
    }

    [Fact]
    public async Task Start_KeepsPollingWhileHotkeyStaysUp()
    {
        (HotkeyKillSwitchMonitor monitor, FakeNativeApi native, KillSwitch killSwitch) = Build();
        using (monitor)
        using (killSwitch)
        {
            monitor.Start();

            await Task.Delay(30);

            // Still watching, not yet triggered.
            Assert.True(monitor.IsRunning);
            Assert.False(killSwitch.IsTriggered);

            native.KeysDown.Add(VirtualKey.F12);
            await WaitFor(() => killSwitch.IsTriggered);

            Assert.True(killSwitch.IsTriggered);
        }
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 500 && !condition(); i++)
        {
            await Task.Delay(5);
        }
    }
}
