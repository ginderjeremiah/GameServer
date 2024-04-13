namespace GameLibrary.Logging
{
    public class ApiLogger : IApiLogger
    {
        private readonly ApiLogWorker _logWorker = new();

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
            _logWorker.Enqueue(new LogMessage(log, color));
        }
    }
}
