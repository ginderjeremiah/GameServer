using Game.Abstractions.Contracts;
using Game.Application.Services;

namespace Game.Api.Models.Enemies
{
    public class EnemyInstance : IModelFromSource<EnemyInstance, BattleStartResult>
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public uint Seed { get; set; }
        public required List<int> SelectedSkills { get; set; }

        /// <summary>
        /// Projects a battle-start result onto the wire model. The single source of truth for the
        /// enemy-instance projection shared by the <c>NewEnemy</c> and <c>ChallengeBoss</c> socket commands,
        /// keeping the two from drifting (#492).
        /// </summary>
        public static EnemyInstance FromSource(BattleStartResult source)
        {
            var enemy = source.Enemy;
            return new EnemyInstance
            {
                Id = enemy.Id,
                Level = enemy.Level,
                Seed = source.Seed,
                SelectedSkills = enemy.BattleSkills.Select(skill => skill.Id).ToList(),
                Attributes = enemy.GetAttributeModifiers()
                    .Select(modifier => BattlerAttribute.From(modifier.Attribute, modifier.Amount)),
            };
        }
    }
}
