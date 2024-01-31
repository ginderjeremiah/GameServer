using DataAccess.Models.Items;
using DataAccess.Models.Zones;
using GameLibrary;
using System.Data;

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

            return GetAllZones()[zoneId];
        }

        public bool ValidateZoneId(int zoneId)
        {
            return zoneId >= 0 && zoneId < GetAllZones().Count;
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

            var ds = FillSet(commandText);
            var drops = ds.Tables[1].To<ItemDrop>()
                .GroupBy(drop => drop.DroppedById)
                .ToDictionary(g => g.Key, g => g.ToList());
            return ds.Tables[0]
                .AsEnumerable()
                .Select(row => new Zone()
                {
                    ZoneId = row["ZoneId"].AsInt(),
                    ZoneName = row["ZoneName"].AsString(),
                    ZoneDesc = row["ZoneDesc"].AsString(),
                    ZoneOrder = row["ZoneOrder"].AsInt(),
                    LevelMin = row["LevelMin"].AsInt(),
                    LevelMax = row["LevelMax"].AsInt(),
                    ZoneDrops = drops[row["zoneId"].AsInt()]
                })
                .ToList();
        }
    }

    public interface IZones
    {
        public List<Zone> AllZones();
        public Zone GetZone(int zoneId);
        public bool ValidateZoneId(int zoneId);
    }
}
