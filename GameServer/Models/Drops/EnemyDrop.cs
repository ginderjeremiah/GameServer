namespace GameServer.Models.Drops
{
    public class EnemyDrop : IDrop
    {
        public int EnemyDropId { get; set; }
        public int EnemyId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }
    }
}
