namespace Game.Core.Entities
{
    public partial class EnemyDrop
    {
        public int Id { get; set; }
        public int EnemyId { get; set; }
        public int ItemId { get; set; }
        public decimal DropRate { get; set; }

        public virtual Enemy Enemy { get; set; }
        public virtual Item Item { get; set; }
    }
}
