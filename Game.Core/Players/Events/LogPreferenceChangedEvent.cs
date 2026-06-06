namespace Game.Core.Players.Events
{
    /// <summary>
    /// Raised when a player changes a log preference setting.
    /// </summary>
    public record LogPreferenceChangedEvent(
        int PlayerId,
        ELogType LogType,
        bool Enabled) : IPlayerPersistenceEvent;
}
