using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Game.Api.Logging
{
    public class LogFormatter : ConsoleFormatter, IDisposable
    {
        private readonly IDisposable? _optionsReloadToken;
        private LogFormatterOptions _formatterOptions;

        public LogFormatter(IOptionsMonitor<LogFormatterOptions> options) : base(nameof(LogFormatter))
        {
            (_optionsReloadToken, _formatterOptions) =
                (options.OnChange(ReloadLoggerOptions), options.CurrentValue);
        }

        private void ReloadLoggerOptions(LogFormatterOptions options)
        {
            _formatterOptions = options;
        }

        public override void Write<TState>(
            in LogEntry<TState> logEntry,
            IExternalScopeProvider? scopeProvider,
            TextWriter textWriter)
        {
            string? message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);

            if (message is null)
            {
                return;
            }

            CustomLogicGoesHere(textWriter);
            textWriter.WriteLine(message);
        }

        private void CustomLogicGoesHere(TextWriter textWriter)
        {
            //textWriter.Write(_formatterOptions.CustomPrefix);
        }

        public void Dispose()
        {
            _optionsReloadToken?.Dispose();
        }
    }
}
