namespace Game.Api.Sockets
{
    /// <summary>
    /// The result of running a socket command under the per-socket command lock, returned by
    /// <see cref="SocketHandler"/> so the two execution paths can apply their own fault policy. A server
    /// push escalates a <see cref="Faulted"/> outcome (dead-letter + client re-sync notice); a
    /// <see cref="TornDown"/> outcome is a lifetime cancellation and never escalates.
    /// </summary>
    internal enum SocketCommandOutcome
    {
        /// <summary>The command ran and its response was sent.</summary>
        Succeeded,

        /// <summary>The command ran, but sending its response failed (e.g. the socket closed mid-send).</summary>
        NotDelivered,

        /// <summary>The per-command budget elapsed; a timeout response was sent and the command abandoned.</summary>
        TimedOut,

        /// <summary>The command threw a genuine fault. No response was sent — the caller decides how to surface it.</summary>
        Faulted,

        /// <summary>The command's parameters could not be bound (malformed/missing JSON) — a bad request, not a server fault.</summary>
        MalformedParameters,

        /// <summary>The command was cancelled by a lifetime/teardown unwind, not the per-command timeout.</summary>
        TornDown
    }
}
