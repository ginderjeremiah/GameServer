namespace Game.Api.Models.Drops
{
    internal interface IDrop
    {
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }
    }
}
