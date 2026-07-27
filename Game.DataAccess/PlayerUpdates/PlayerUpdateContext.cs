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
        /// <see cref="DomainEventEnvelope.Unsequenced"/> when it carries none (#2473). Read by
        /// <see cref="PlayerWriteWatermarkGuard"/>, which rejects a write older than its target's watermark.
        /// </summary>
        public long Sequence { get; private set; } = DomainEventEnvelope.Unsequenced;

        /// <summary>
        /// How many of this envelope's write targets <see cref="PlayerWriteWatermarkGuard"/> skipped as
        /// already superseded. Reported (not just dropped) so a genuine reordering storm — or a bug in the
        /// sequencing itself — is observable on a path whose whole purpose is never to silently lose a write;
        /// <see cref="DataProviderSynchronizer"/> reads it after each apply and totals it per drain pass.
        /// </summary>
        public int RejectedTargetCount { get; private set; }

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

        /// <summary>
        /// Records that the guard skipped <paramref name="count"/> of this envelope's targets as superseded.
        /// Additive so a handler that passes through the guard more than once in a single apply totals rather
        /// than overwrites.
        /// </summary>
        public void RecordRejectedTargets(int count)
        {
            RejectedTargetCount += count;
        }
    }
}
