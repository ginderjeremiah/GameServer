using System.Collections.Concurrent;

namespace GameLibrary.Logging
{
    internal class ApiLogWorker
    {
        private const int BATCH_SIZE = 5;

        private static readonly ConcurrentQueue<LogMessage> _logQueue = new();
        private static readonly ConcurrentQueue<LogMessage> _logErrors = new();
        private static readonly AutoResetEvent _resetEvent = new(false);
        private static readonly object _lock = new();
        private static ApiLogWorker _worker;

        public ApiLogWorker()
        {
            if (_worker is null)
            {
                lock (_lock)
                {
                    if (_worker is null)
                    {
                        _worker = this;
                        Task.Run(ProcessLogs);
                    }
                }
            }
        }

        public void Enqueue(LogMessage logMessage)
        {
            _logQueue.Enqueue(logMessage);
            if (_logQueue.Count >= BATCH_SIZE)
            {
                _resetEvent.Set();
            }
        }

        private void ProcessLogs()
        {
            while (_resetEvent.WaitOne())
            {
                while (_logQueue.TryDequeue(out var log))
                {
                    try
                    {
                        Log(log);
                        if (!_logErrors.IsEmpty)
                            EnqueueErrors();
                    }
                    catch (Exception ex)
                    {
                        if (log.LoggingError is null)
                        {
                            log.LoggingError = $"Logging Error: {ex.Message}\nTrace: {ex.StackTrace}";
                            _logErrors.Enqueue(log);
                        }
                    }
                }
            }
        }

        private void Log(LogMessage logMessage)
        {
            if (logMessage.Color is not null)
                Console.ForegroundColor = logMessage.Color.Value;

            Console.WriteLine(logMessage.Message);

            if (logMessage.Color is not null)
                Console.ResetColor();
        }

        private void EnqueueErrors()
        {
            foreach (var error in _logErrors)
            {
                _logQueue.Enqueue(error);
            }
        }
    }


}
