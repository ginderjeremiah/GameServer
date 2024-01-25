using DataAccess.Models.Items;
using DataAccess.Models.Zones;
using GameLibrary;
using System.Data;

namespace DataAccess.Repositories
{
    internal class Zones : BaseRepository, IZones
    {
        public Zones(string connectionString) : base(connectionString) { }

        public List<Zone> AllZones()
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
    }
}
