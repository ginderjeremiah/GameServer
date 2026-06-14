namespace Game.DataAccess
{
    /// <summary>
    /// Scoped buffer that collects the player persistence events raised during a single
    /// <see cref="Repositories.PlayerRepository.SavePlayer"/> so they can be flushed to the write-behind
    /// queue as one batched LPUSH instead of one round-trip per event. <see cref="PlayerPersistencePublisher"/>
    /// fills it as each event is dispatched; <c>SavePlayer</c> drains and publishes it after the dispatch
    /// settles. It is registered scoped so the publisher (constructed per dispatch) and the repository share
    /// the same instance within a request scope, and <see cref="Drain"/> clears it so a second save in the
    /// same scope starts fresh.
    /// </summary>
    internal sealed class PlayerUpdateBatch
    {
        private readonly List<DomainEventEnvelope> _events = [];

        public void Add(DomainEventEnvelope envelope)
        {
            _events.Add(envelope);
        }

        /// <summary>
        /// Returns the buffered events and clears the buffer so it is ready for the next save.
        /// </summary>
        public IReadOnlyList<DomainEventEnvelope> Drain()
        {
            if (_events.Count == 0)
            {
                return [];
            }

            var drained = _events.ToArray();
            _events.Clear();
            return drained;
        }
    }
}
