using GameCore.Logging.Interfaces;
using System.Collections.Concurrent;
using static GameCore.Logging.LogLevel;
using static System.ConsoleColor;

namespace GameCore.Logging
{
    internal class ApiLogWorker
    {
        private const int BATCH_SIZE = 5;

        private readonly ConcurrentQueue<LogMessage> _logQueue = new();
        private readonly ConcurrentQueue<LogMessage> _logErrors = new();
        private readonly BackgroundWorker _worker;

        public ApiLogWorker(IApiLogger logger)
        {
            _worker = new BackgroundWorker(logger, ProcessLogs)
            {
                Name = nameof(ApiLogWorker)
            };
        }

        public void Enqueue(LogMessage logMessage)
        {
            _logQueue.Enqueue(logMessage);
            if (_logQueue.Count >= BATCH_SIZE && !_worker.IsRunning)
            {
                _worker.Start();
            }
        }

        private void ProcessLogs()
        {
            while (_logQueue.TryDequeue(out var log))
            {
                try
                {
                    Console.ForegroundColor = GetColorByLogLevel(log.Level);
                    Console.WriteLine(log.Message);
                    if (!_logErrors.IsEmpty)
                        EnqueueErrors();
                }
                catch (Exception ex)
                {
                    if (log.LoggingError is null)
                    {
                        log.LoggingError = ex.GetDetails();
                        _logErrors.Enqueue(log);
                    }
                }
            }
        }

        private void EnqueueErrors()
        {
            foreach (var error in _logErrors)
            {
                _logQueue.Enqueue(error);
            }
        }

        private static ConsoleColor GetColorByLogLevel(LogLevel level)
        {
            return level switch
            {
                Full => Green,
                Debug => Gray,
                Info => White,
                Warning => Yellow,
                Error => Red,
                Fatal => DarkRed,
                _ => Cyan
            };
        }
    }
}
