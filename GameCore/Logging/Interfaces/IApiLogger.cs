namespace GameCore.Logging.Interfaces
{
    public interface IApiLogger
    {
        public void Log(object log, LogLevel level = LogLevel.Full);
        public void LogDebug(object log);
        public void LogInfo(object log);
        public void LogWarning(object log);
        public void LogWarning(Exception exception);
        public void LogError(object log);
        public void LogError(Exception exception);
        public void LogFatal(object log);
        public void LogFatal(Exception exception);
    }
}
