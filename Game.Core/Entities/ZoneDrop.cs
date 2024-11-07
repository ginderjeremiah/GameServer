namespace Game.Core.Entities
{
    public partial class ZoneDrop
    {
        public int Id { get; set; }
        public int ZoneId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public virtual Zone Zone { get; set; }
        public virtual Item Item { get; set; }
    }
}
