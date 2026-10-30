using Core.Scripting;

namespace Core.Tests.Fakes;

public sealed class RecordingLogger : IScriptLogger
{
    public List<string> Infos { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<(string Message, Exception? Exception)> Errors { get; } = [];

    public void Info(string message) => Infos.Add(message);

    public void Warn(string message) => Warnings.Add(message);

    public void Error(string message, Exception? exception = null) => Errors.Add((message, exception));
}
