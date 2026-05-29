using Game.Core.Events;
using Microsoft.Extensions.Logging;

namespace Game.Application
{
    public class LoggingEventHandler(ILogger<LoggingEventHandler> logger) : IDomainEventHandler<IDomainEvent>
    {
        private readonly ILogger<LoggingEventHandler> _logger = logger;

        public Task HandleAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Domain event dispatched: {EventType} — {Event}", domainEvent.GetType().Name, domainEvent);
            return Task.CompletedTask;
        }
    }
}
