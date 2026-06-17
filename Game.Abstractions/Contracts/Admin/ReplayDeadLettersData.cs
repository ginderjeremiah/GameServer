namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// Selects which dead-letter entries to replay back onto the player update queue: either every entry
    /// (<see cref="All"/>) or a specific set identified by their exact raw payloads (<see cref="Payloads"/>,
    /// as returned by the inspection surface).
    /// </summary>
    public class ReplayDeadLettersData
    {
        /// <summary>Replay every entry currently on the dead-letter queue. When true, <see cref="Payloads"/> is ignored.</summary>
        public bool All { get; set; }

        /// <summary>The exact raw payloads to replay. Ignored when <see cref="All"/> is true.</summary>
        public List<string>? Payloads { get; set; }
    }
}
