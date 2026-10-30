using Core.Scripting;

namespace Client.Logging;

/// <summary>Writes script output to the console.</summary>
internal sealed class ConsoleScriptLogger : IScriptLogger
{
    public void Info(string message) => Write(ConsoleColor.Gray, "INFO", message);

    public void Warn(string message) => Write(ConsoleColor.Yellow, "WARN", message);

    public void Error(string message, Exception? exception = null) =>
        Write(ConsoleColor.Red, "ERROR", exception is null ? message : $"{message} {exception}");

    private static void Write(ConsoleColor color, string level, string message)
    {
        Console.ForegroundColor = color;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {level}: {message}");
        Console.ResetColor();
    }
}
