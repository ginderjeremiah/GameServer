namespace Game.Infrastructure.Entities
{
    public class Skill : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public required string Description { get; set; }
        public int CooldownMs { get; set; }
        public required string IconPath { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual List<SkillDamageMultiplier> SkillDamageMultipliers { get => field ?? throw new NotLoadedException(nameof(SkillDamageMultipliers)); set; }
        public virtual List<SkillEffect> SkillEffects { get => field ?? throw new NotLoadedException(nameof(SkillEffects)); set; }
        public virtual List<EnemySkill> EnemySkills { get => field ?? throw new NotLoadedException(nameof(EnemySkills)); set; }
        public virtual List<PlayerSkill> PlayerSkills { get => field ?? throw new NotLoadedException(nameof(PlayerSkills)); set; }
    }
}
