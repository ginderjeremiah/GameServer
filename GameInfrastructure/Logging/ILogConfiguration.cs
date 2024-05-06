using GameCore;

namespace GameInfrastructure.Logging
{
    public interface ILogConfiguration
    {
        public LogLevel MinimumLevel { get; }
    }
}
