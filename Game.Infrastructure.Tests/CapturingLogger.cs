using Microsoft.Extensions.Logging;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// A minimal <see cref="ILogger"/> test double that records each call's level and exception. Shared by tests
    /// whose only observable effect on a caught fault is the error log (<see cref="RedisCommandBudgetTests"/>,
    /// <see cref="RedisMultiplexerFactoryTests"/>), so the capture logic isn't duplicated per test class.
    /// </summary>
    internal sealed class CapturingLogger : ILogger
    {
        private readonly object _gate = new();
        private readonly List<(LogLevel Level, Exception? Exception)> _entries = [];

        public IReadOnlyList<(LogLevel Level, Exception? Exception)> Entries
        {
            get
            {
                lock (_gate)
                {
                    return _entries.ToList();
                }
            }
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _entries.Add((logLevel, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
