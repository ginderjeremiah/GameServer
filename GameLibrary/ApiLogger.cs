namespace GameLibrary
{
    public class ApiLogger : IApiLogger
    {
        private const int BATCH_SIZE = 5;

        private static readonly LinkedList<LogMessage> _logQueue = new();
        private static bool _isLogging = false;
        private static readonly object _logLock = new();

        public void Log(object log)
        {
            EnqueueLog(log.AsString());
        }

        public void LogError(object log)
        {
            EnqueueLog(log.AsString(), ConsoleColor.Red);
        }

        public void LogError(Exception exception)
        {
            LogError($"{exception.GetType()}: {exception.Message}\nStack Trace: {exception.StackTrace}");
        }

        private void EnqueueLog(string log, ConsoleColor? color = null)
        {
            _logQueue.AddLast(new LogMessage()
            {
                Message = $"{DateTime.Now:HH:mm:ss:fff}: {log}",
                Color = color
            });
            if (_logQueue.Count >= BATCH_SIZE && !_isLogging)
            {
                Task.Run(ProcessQueue);
            }
        }

        private void ProcessQueue()
        {
            lock (_logLock)
            {
                _isLogging = true;
                while (_logQueue.First is not null)
                {
                    var logMessage = _logQueue.First.Value;

                    if (logMessage.Color is not null)
                        Console.ForegroundColor = logMessage.Color.Value;

                    Console.WriteLine(logMessage.Message);

                    if (logMessage.Color is not null)
                        Console.ResetColor();

                    _logQueue.RemoveFirst();
                }
                _isLogging = false;
            }
        }

        private class LogMessage
        {
            public string Message { get; set; }
            public ConsoleColor? Color { get; set; }
        }
    }
}
