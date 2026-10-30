using Core.Windows;

namespace Core.Tests.Fakes;

public sealed class FakeWindowFinder : IWindowFinder
{
    private readonly Dictionary<string, nint> _windows = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Queries { get; } = [];

    public FakeWindowFinder Register(string processName, nint handle)
    {
        _windows[processName] = handle;
        return this;
    }

    public nint FindMainWindow(string processName)
    {
        Queries.Add(processName);
        return _windows.GetValueOrDefault(processName);
    }
}
