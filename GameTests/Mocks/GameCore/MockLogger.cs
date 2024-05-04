using GameCore;
using GameCore.Logging;
using GameCore.Logging.Interfaces;
using static GameCore.Logging.LogLevel;

namespace GameTests.Mocks.GameCore
{
    internal class MockLogger : IApiLogger
    {
        private LinkedList<string> Logs { get; set; } = new();
        private LinkedList<string> ErrorLogs { get; set; } = new();

        private Task<string?>? TaskAwaitingLog { get; set; }
        private Task<string?>? TaskAwaitingErrorLog { get; set; }

        public void Log(object message, LogLevel level = Full)
        {
            Console.WriteLine(message);
            Logs.AddLast(message.ToString());
            TaskAwaitingLog?.Start();
        }

        public void LogDebug(object message)
        {
            Log(message);
        }

        public void LogInfo(object message)
        {
            Log(message);
        }

        public void LogWarning(object message)
        {
            LogError(message);
        }

        public void LogWarning(Exception exception)
        {
            LogError(exception);
        }

        public void LogError(object message)
        {
            Console.WriteLine(message);
            ErrorLogs.AddLast(message.AsString());
            TaskAwaitingErrorLog?.Start();
        }

        public void LogError(Exception exception)
        {
            Console.WriteLine(exception.Message);
            ErrorLogs.AddLast(exception.Message);
            TaskAwaitingErrorLog?.Start();
        }

        public void LogFatal(object message)
        {
            LogError(message);
        }

        public void LogFatal(Exception exception)
        {
            LogError(exception);
        }

        public async Task<string?> AwaitNextLog()
        {
            TaskAwaitingLog = new Task<string?>(ConsumeLog);
            if (Logs.First is not null)
            {
                TaskAwaitingLog.Start();
            }
            return await TaskAwaitingLog;
        }

        public async Task<string?> AwaitNextErrorLog()
        {
            TaskAwaitingErrorLog = new Task<string?>(ConsumeErrorLog);
            if (ErrorLogs.First is not null)
            {
                TaskAwaitingErrorLog.Start();
            }
            return await TaskAwaitingErrorLog;
        }

        private string? ConsumeLog()
        {
            var log = Logs.First?.Value;
            Logs.RemoveFirst();
            return log;
        }

        private string? ConsumeErrorLog()
        {
            var log = ErrorLogs.First?.Value;
            ErrorLogs.RemoveFirst();
            return log;
        }
    }
}
