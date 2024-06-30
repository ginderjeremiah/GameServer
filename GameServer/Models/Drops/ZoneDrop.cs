namespace GameServer.Models.Drops
{
    public class ZoneDrop : IDrop
    {
        public int ZoneDropId { get; set; }
        public int ZoneId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }
    }
}
