using GameCore.Logging.Interfaces;
using static GameCore.Logging.LogLevel;

namespace GameCore.Logging
{
    public class ApiLogger : IApiLogger
    {
        private readonly ApiLogWorker _logWorker;
        private readonly LogLevel MinimumLevel;

        public ApiLogger(ILogConfiguration config)
        {
            MinimumLevel = config.MinimumLevel;
            _logWorker = ApiLogWorkerFactory.GetWorker(this);
        }

        public void Log(object log, LogLevel level = Full)
        {
            EnqueueLog(log.AsString(), level);
        }

        public void LogDebug(object log)
        {
            EnqueueLog(log.AsString(), Debug);
        }

        public void LogInfo(object log)
        {
            EnqueueLog(log.AsString(), Info);
        }

        public void LogWarning(object log)
        {
            EnqueueLog(log.AsString(), Warning);
        }

        public void LogWarning(Exception exception)
        {
            LogWarning(exception.GetDetails());
        }

        public void LogError(object log)
        {
            EnqueueLog(log.AsString(), Error);
        }

        public void LogError(Exception exception)
        {
            LogError(exception.GetDetails());
        }

        public void LogFatal(object log)
        {
            EnqueueLog(log.AsString(), Fatal);
        }

        public void LogFatal(Exception exception)
        {
            LogFatal(exception.GetDetails());
        }

        private void EnqueueLog(string log, LogLevel level)
        {
            if (level >= MinimumLevel)
            {
                _logWorker.Enqueue(new LogMessage(log, level));
            }
        }
    }
}
