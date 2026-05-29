using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.TestInfrastructure.Helpers;

public class XunitLoggerProvider : ILoggerProvider
{
    private readonly ITestOutputHelper _testOutputHelper;

    public XunitLoggerProvider(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public ILogger CreateLogger(string categoryName) => new XunitLogger(_testOutputHelper, categoryName);
    public void Dispose() { }
}

public class XunitLogger : ILogger
{
    private readonly ITestOutputHelper _output;
    private readonly string _categoryName;

    public XunitLogger(ITestOutputHelper output, string categoryName)
    {
        _output = output;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        try
        {
            // Format the message exactly how you want it to appear in the Test Detail Summary
            var message = $"{logLevel}: [{_categoryName}] {formatter(state, exception)}";
            if (exception != null)
            {
                message += $"\n{exception}";
            }

            _output.WriteLine(message);
        }
        catch (InvalidOperationException)
        {
            // Catches cases where the test has already finished but background tasks are still logging
        }
    }
}