namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// How serious a <see cref="ContentHealthFinding"/> is — the client-facing mirror of the application-tier
    /// content-graph lint severity. A <see cref="Warning"/> is unreachable / dead content the runtime tolerates;
    /// an <see cref="Error"/> is a genuine break (a dangling reference, or live content wedged into a
    /// permanently unusable state) that also gates the content-lint CI build.
    /// </summary>
    public enum EContentHealthSeverity
    {
        Warning = 0,
        Error = 1,
    }
}
