using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>
    /// Read/write contract for a challenge. Shared by the reference-data read path (the
    /// <c>GetChallenges</c> socket command) and the admin Content Authoring write path
    /// (<c>AddEditChallenges</c>). <see cref="StatisticType"/> and <see cref="EntityType"/> are
    /// derived from <see cref="ChallengeTypeId"/> on read and ignored on write.
    /// </summary>
    public class Challenge : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EChallengeType ChallengeTypeId { get; set; }
        public EStatisticType? StatisticType { get; set; }
        public EEntityType EntityType { get; set; }
        public int? TargetEntityId { get; set; }
        public decimal ProgressGoal { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
