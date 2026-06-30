using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>
    /// Read/write contract for one weighted slice of a skill's direct-hit damage (spike #1343); see
    /// <see cref="Core.Skills.SkillDamagePortion"/>. Doubles as the change-item for the portions child-saver.
    /// </summary>
    public class SkillDamagePortion : IModel
    {
        /// <summary>The leaf damage type this slice deals.</summary>
        public EDamageType Type { get; set; }

        /// <summary>The raw (un-normalized) weight of this slice within the hit.</summary>
        public decimal Weight { get; set; }
    }
}
