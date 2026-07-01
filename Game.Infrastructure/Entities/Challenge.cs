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

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual Item? RewardItem { get; set; }
        public virtual ItemMod? RewardItemMod { get; set; }
        public virtual ChallengeType ChallengeType { get => field ?? throw new NotLoadedException(nameof(ChallengeType)); set; }
    }
}
