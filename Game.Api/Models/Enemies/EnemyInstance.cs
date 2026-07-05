using Game.Abstractions.Contracts;
using Game.Application.Services;
using Game.Core.Battle;

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
        /// Non-null when this battle was already in progress rather than freshly started (#1595): the real
        /// elapsed time (ms) since it began, which the client must fast-forward through — replay-to-offset,
        /// #1597 — before continuing live. Null for a freshly started battle.
        /// </summary>
        public int? ElapsedOffsetMs { get; set; }

        /// <summary>
        /// The enemy's combat-rating capability measure (<see cref="CombatRating.Rate"/>, spike #1526
        /// Decision 7), rated on its fielded <c>BattleSkills</c> loadout — display-only, never recomputed
        /// client-side (no parity surface).
        /// </summary>
        public double EnemyRating { get; set; }

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
                ElapsedOffsetMs = source.ElapsedOffsetMs,
                EnemyRating = CombatRating.Rate(enemy.ToBattler(), isPlayer: false),
            };
        }
    }
}
