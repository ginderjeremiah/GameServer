using Game.Core.Entities;

namespace Game.Core.BattleSimulation
{
    /// <summary>
    /// Represents additional data for an enemy specific to an instance.
    /// </summary>
    public class EnemyInstance
    {
        /// <summary>
        /// The corresponding <see cref="Enemy.Id"/>
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// The level of the enemy.
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// The attributes of an enemy generated from a <see cref="AttributeDistribution"/>.
        /// </summary>
        public List<BattlerAttribute> Attributes { get; set; }

        /// <summary>
        /// The seed for the enemy used for any RNG calculations during battle.
        /// </summary>
        public uint Seed { get; set; }

        /// <summary>
        /// The skill ids for the skills that the enemy will use during battle.
        /// </summary>
        public List<int> SelectedSkills { get; set; }

        /// <summary>
        /// Creates a new enemy with all properties initialized.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="level"></param>
        /// <param name="attributes"></param>
        /// <param name="seed"></param>
        /// <param name="selectedSkills"></param>
        public EnemyInstance(int id, int level, List<BattlerAttribute> attributes, uint seed, List<int> selectedSkills)
        {
            Id = id;
            Level = level;
            Attributes = attributes;
            Seed = seed;
            SelectedSkills = selectedSkills;
        }

        /// <summary>
        /// Returns a hash of all the information for this instance.
        /// </summary>
        /// <returns>A hash <see cref="string"/>.</returns>
        public string Hash()
        {
            var data = $"{Id}{Level}{string.Join(",", Attributes.Select(att => (double)att.Amount))}";
            return data.Hash(Seed.ToString(), 1);
        }
    }
}
