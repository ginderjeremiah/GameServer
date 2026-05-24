namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Bounded-context repository for world/combat reference data: enemies and zones.
    /// </summary>
    public interface IWorldRepository
    {
        IEnemies Enemies { get; }
        IZones Zones { get; }
    }
}
