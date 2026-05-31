namespace Game.Abstractions.Entities
{
    public class Attribute : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        public virtual List<AttributeDistribution> AttributeDistributions { get => field ?? throw new NotLoadedException(nameof(AttributeDistributions)); set; }
        public virtual List<ItemAttribute> ItemAttributes { get => field ?? throw new NotLoadedException(nameof(ItemAttributes)); set; }
        public virtual List<ItemModAttribute> ItemModAttributes { get => field ?? throw new NotLoadedException(nameof(ItemModAttributes)); set; }
        public virtual List<PlayerAttribute> PlayerAttributes { get => field ?? throw new NotLoadedException(nameof(PlayerAttributes)); set; }
        public virtual List<SkillDamageMultiplier> SkillDamageMultipliers { get => field ?? throw new NotLoadedException(nameof(SkillDamageMultipliers)); set; }
    }
}
