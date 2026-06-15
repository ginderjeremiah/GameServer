using Game.Core.Events;
using Microsoft.Extensions.Logging;

namespace Game.Application
{
    public class LoggingEventHandler(ILogger<LoggingEventHandler> logger) : IDomainEventHandler<IDomainEvent>
    {
        private readonly ILogger<LoggingEventHandler> _logger = logger;

        public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            // Never serialize the event itself — an event may carry a whole aggregate (e.g. the Player on
            // BattleCompletedEvent), which default object formatting would pull into the logs along with
            // player identity, stats, and inventory. Log only the type name, plus a curated set of safe
            // scalar ids attached as a structured scope when the event opts in via ILoggableDomainEvent.
            using var scope = domainEvent is ILoggableDomainEvent loggable
                ? _logger.BeginScope(loggable.GetLogProperties())
                : null;
            _logger.LogDebug("Domain event dispatched: {EventType}", domainEvent.GetType().Name);

            return Task.CompletedTask;
        }
    }
}
