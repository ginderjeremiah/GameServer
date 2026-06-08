using Microsoft.Extensions.Logging;

namespace Game.TestInfrastructure.Helpers;

public record CapturingEntry(
    string Category,
    LogLevel Level,
    string Message,
    IReadOnlyList<object?> ScopeStates,
    IReadOnlyList<KeyValuePair<string, object?>> Properties);

public class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<CapturingEntry> _entries = [];
    private readonly object _lock = new();

    public IReadOnlyList<CapturingEntry> Entries
    {
        get { lock (_lock) { return [.. _entries]; } }
    }

    public ILogger CreateLogger(string categoryName) =>
        new CapturingLogger(categoryName, _entries, _lock);

    public void Dispose() { }
}

internal class CapturingLogger(string categoryName, List<CapturingEntry> entries, object lockObj) : ILogger
{
    private readonly AsyncLocal<List<object?>> _scopeStack = new();

    private List<object?> ScopeStack => _scopeStack.Value ??= [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        var stack = ScopeStack;
        stack.Add(state);
        return new ScopeHandle(() =>
        {
            if (stack.Count > 0)
            {
                stack.RemoveAt(stack.Count - 1);
            }
        });
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> kvps
            ? (IReadOnlyList<KeyValuePair<string, object?>>)kvps.ToList()
            : [];

        var entry = new CapturingEntry(
            categoryName,
            logLevel,
            formatter(state, exception),
            [.. ScopeStack],
            properties);

        lock (lockObj)
        {
            entries.Add(entry);
        }
    }

    private sealed class ScopeHandle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
