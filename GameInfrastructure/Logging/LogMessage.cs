using GameCore;

namespace GameInfrastructure.Logging
{
    internal class LogMessage
    {
        private readonly string _message;
        public LogLevel Level { get; set; }
        public DateTime TimeStamp { get; set; }
        public string? LoggingError { get; set; }

        public string Message => $"{TimeStamp:yyyy-MM-dd'T'HH:mm:ss:fff}: {_message}";

        public LogMessage(string message, LogLevel level)
        {
            _message = message;
            Level = level;
            TimeStamp = DateTime.UtcNow;
        }
    }
}
