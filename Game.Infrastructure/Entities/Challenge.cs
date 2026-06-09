namespace Game.Infrastructure.Entities
{
    public class Challenge : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int ChallengeTypeId { get; set; }
        public int? TargetEntityId { get; set; }
        public decimal ProgressGoal { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
        public int? RewardSkillId { get; set; }

        public virtual Item? RewardItem { get; set; }
        public virtual ItemMod? RewardItemMod { get; set; }
        public virtual Skill? RewardSkill { get; set; }
        public virtual ChallengeType ChallengeType { get => field ?? throw new NotLoadedException(nameof(ChallengeType)); set; }
    }
}
