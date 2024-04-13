namespace DataAccess.Entities.Drops
{
    interface IDrop : IEntity
    {
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }
    }
}
