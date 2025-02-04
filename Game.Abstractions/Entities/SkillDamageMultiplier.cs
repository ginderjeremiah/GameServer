﻿namespace Game.Abstractions.Entities
{
    public partial class SkillDamageMultiplier
    {
        public int SkillId { get; set; }
        public int AttributeId { get; set; }
        public decimal Multiplier { get; set; }

        public virtual Skill Skill { get; set; }
        public virtual Attribute Attribute { get; set; }
    }
}
