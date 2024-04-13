using DataAccess.Entities.Drops;
using DataAccess.Entities.Zones;

namespace DataAccess.Repositories
{
    internal class Zones : BaseRepository, IZones
    {
        private static List<Zone>? _zoneList;

        public Zones(string connectionString) : base(connectionString) { }

        public List<Zone> AllZones()
        {
            return _zoneList ??= GetAllZones();
        }

        public Zone GetZone(int zoneId)
        {
            if (!ValidateZoneId(zoneId))
            {
                throw new ArgumentOutOfRangeException(nameof(zoneId));
            }

            return AllZones()[zoneId];
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < AllZones().Count;
        }

        private List<Zone> GetAllZones()
        {
            var commandText = @"
                SELECT
	                ZoneId,
	                ZoneName,
	                ZoneDesc,
	                ZoneOrder,
                    LevelMin,
                    LevelMax
                FROM Zones
                ORDER BY ZoneId

                SELECT
                    ZoneId AS DroppedById,
                    ItemId,
                    DropRate
                FROM ZoneDrops";

            var result = QueryToList<Zone, ItemDrop>(commandText);

            var drops = result.Item2
                .GroupBy(drop => drop.DroppedById)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var zone in result.Item1)
            {
                zone.ZoneDrops = drops[zone.ZoneId];
            }

            return result.Item1;
        }
    }

    public interface IZones
    {
        public List<Zone> AllZones();
        public Zone GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
    }
}
