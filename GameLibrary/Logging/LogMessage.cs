namespace GameLibrary.Logging
{
    internal class LogMessage
    {
        private readonly string _message;
        public ConsoleColor? Color { get; set; }
        public DateTime TimeStamp { get; set; }
        public string? LoggingError { get; set; }

        public string Message => $"{TimeStamp:O}: {_message}";

        public LogMessage(string message, ConsoleColor? color)
        {
            _message = message;
            Color = color;
            TimeStamp = DateTime.Now;
        }
    }
}
