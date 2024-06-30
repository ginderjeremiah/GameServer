namespace GameServer.Models.Drops
{
    interface IDrop
    {
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }
    }
}
