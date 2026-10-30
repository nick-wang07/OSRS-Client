namespace Core.Scripting;

/// <summary>Where scripts report what they are doing.</summary>
public interface IScriptLogger
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
