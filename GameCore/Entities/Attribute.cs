namespace GameCore.Entities
{
    public class Attribute
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public virtual List<AttributeDistribution> AttributeDistributions { get; set; }
        public virtual List<ItemAttribute> ItemAttributes { get; set; }
        public virtual List<ItemModAttribute> ItemModAttributes { get; set; }
        public virtual List<PlayerAttribute> PlayerAttributes { get; set; }
        public virtual List<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
    }
}
