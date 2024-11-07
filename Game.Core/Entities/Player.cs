namespace Game.Core.Entities
{
    public partial class Player
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public Guid Salt { get; set; }
        public string PassHash { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }

        public virtual List<PlayerAttribute> PlayerAttributes { get; set; }
        public virtual List<InventoryItem> InventoryItems { get; set; }
        public virtual List<LogPreference> LogPreferences { get; set; }
        public virtual List<PlayerSkill> PlayerSkills { get; set; }
    }
}
