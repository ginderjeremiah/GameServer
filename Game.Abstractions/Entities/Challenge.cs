namespace Game.Abstractions.Entities
{
    public class Challenge : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ChallengeTypeId { get; set; }
        public int? TargetEntityId { get; set; }
        public int TargetCount { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }

        public virtual Item? RewardItem { get; set; }
        public virtual ItemMod? RewardItemMod { get; set; }
    }
}
