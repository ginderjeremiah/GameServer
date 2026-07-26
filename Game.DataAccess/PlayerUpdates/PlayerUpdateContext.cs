namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Envelope-level metadata for the write-behind event currently being applied, exposed to the handlers
    /// through the drain scope rather than as a positional parameter on
    /// <see cref="IPlayerUpdateHandler{TEvent}.HandleAsync"/> — that signature speaks the event's own payload,
    /// and widening it would touch all fifteen handlers to serve the few that read the metadata.
    /// <para>
    /// <see cref="DataProviderSynchronizer"/> creates a fresh scope per apply (and per retry attempt) and
    /// <see cref="PlayerUpdateEventDispatcher"/> populates this before invoking the handler, so an instance
    /// only ever describes the one envelope its scope was created for.
    /// </para>
    /// </summary>
    internal sealed class PlayerUpdateContext
    {
        /// <summary>
        /// The producing aggregate's write sequence for the envelope being applied, or
        /// <see cref="DomainEventEnvelope.Unsequenced"/> when it carries none (#2473). No handler reads it yet
        /// — the guard that rejects a write older than its target's watermark is #2474.
        /// </summary>
        public long Sequence { get; private set; } = DomainEventEnvelope.Unsequenced;

        /// <summary>
        /// Populates the context from the envelope about to be applied. One method rather than a setter per
        /// field, so later metadata extends this call rather than adding a second one the dispatcher could
        /// forget — and so the dispatcher's call site reads as "describe this envelope" rather than as an
        /// assignment whose completeness the reader has to verify.
        /// </summary>
        public void Describe(DomainEventEnvelope envelope)
        {
            Sequence = envelope.Sequence;
        }
    }
}
