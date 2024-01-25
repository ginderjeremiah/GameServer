namespace GameLibrary
{
    public interface IApiLogger
    {
        public void Log(object log);
        public void LogError(object log);
        public void LogError(Exception exception);
    }
}
