using Game.Core.Attributes.Modifiers;
using Game.Core.Enemies;

namespace Game.Core.Battle
{
    /// <summary>
    /// Represents the rewards that a player receives after defeating an enemy.
    /// </summary>
    public class DefeatRewards
    {
        public int ExpReward { get; set; }

        /// <summary>
        /// The enemy-authored bounty curve factor the exp reward scales by (spike #1526 Decision 4):
        /// <c>min(EnemyRating ÷ PlayerRating, 1)²</c>. It is <c>1</c> at or above a matched fight (the reward
        /// saturates at the enemy's bounty rather than paying an upward premium for punching up) and quadratically
        /// smaller for a trivial enemy (anti-grind). The old ±20% flat band, the upward quadratic premium, and the
        /// <see cref="ServerGameConstants.MaxExpRewardMultiplier"/> cap all dissolve into this one continuous,
        /// self-capping curve. Drives the exp reward only; the effect-based proficiency accrual normalizes by
        /// <c>max(PlayerRating, EnemyRating)</c> directly (spike #1526 Decision 5) rather than reading this factor.
        /// </summary>
        public double DifficultyMultiplier { get; }

        /// <summary>
        /// The player's combat-rating capability measure for this battle (<see cref="CombatRating.Rate"/> against
        /// the battle-simulated <see cref="Battler"/>), superseding the old <c>SumCoreAttributes</c> sum (spike
        /// #1526). Always strictly positive (the rating's own degenerate-guard floor).
        /// </summary>
        public double PlayerRating { get; }

        /// <summary>
        /// The enemy's combat-rating capability measure for this battle, rated on its <em>fielded</em>
        /// <see cref="Enemy.BattleSkills"/> loadout (the draw actually fought, spike #1526 Decision 6) rather than
        /// its full authored pool. Always strictly positive.
        /// </summary>
        public double EnemyRating { get; }

        /// <summary>
        /// Computes the rewards for defeating <paramref name="enemy"/>. Both combatants are rated from the
        /// assembled <see cref="Battler"/>s actually fought: <paramref name="playerBattler"/> reconstructed from
        /// the battle snapshot (<see cref="BattleSnapshot.ToBattler"/>) rather than the live player aggregate, so
        /// the reward is consistent with the snapshot the battle was simulated against; <paramref name="enemy"/>'s
        /// rating is read via <see cref="Enemy.ToBattler"/>, which requires its battle loadout to already be
        /// selected (<see cref="Enemy.SelectBattleSkills"/>/<see cref="Enemy.SetBattleSkills"/>) — true for every
        /// production call site by the time a victory is recorded.
        /// </summary>
        public DefeatRewards(Battler playerBattler, Enemy enemy)
            : this(CombatRating.Rate(playerBattler, isPlayer: true), CombatRating.Rate(enemy.ToBattler(), isPlayer: false))
        {
        }

        /// <summary>
        /// Computes the rewards directly from already-known combat ratings, skipping the
        /// <see cref="CombatRating.Rate"/> re-derivation the <see cref="Battler"/>-based constructor performs. For
        /// a caller that replays many battles against an unchanged player build and/or a deterministic enemy (the
        /// offline simulator, #1730 — the player's rating is invariant between level-ups, and a boss's rating is
        /// invariant for the whole run), computing each rating once and passing it here is the identical reward
        /// math with the redundant rating work removed.
        /// </summary>
        public DefeatRewards(double playerRating, double enemyRating)
        {
            PlayerRating = playerRating;
            EnemyRating = enemyRating;

            // (enemyRating / playerRating)² is the time-to-kill ratio (spike #1526 Decision 1), so clamping the
            // ratio at 1 before squaring is what makes the curve saturate at a matched-or-easier fight rather than
            // paying an upward premium for punching up.
            var ratio = EnemyRating / PlayerRating;
            DifficultyMultiplier = Math.Pow(Math.Min(ratio, 1.0), 2);
            ExpReward = ToIntReward(ServerGameConstants.XpScaleK * EnemyRating * DifficultyMultiplier);
        }

        /// <summary>
        /// The legacy <c>ratio²</c> band/clamp factor the old <c>SumCoreAttributes</c>-based exp reward used to
        /// scale by — no longer called by this class's own constructor (superseded by the combat-rating bounty
        /// curve, spike #1526 Decision 4). Retained public and unchanged so the combat-rating calibration report
        /// (#1533) can fold the real old-curve payout into its anchor XP when comparing the outgoing measure
        /// against the incoming <see cref="CombatRating"/>.
        /// </summary>
        public static double GetDifficultyMultiplier(double enemyAttTotal, double playerAttTotal)
        {
            // No player investment yet: fall back to a neutral multiplier (the reward is then the floored
            // enemy total), matching the original guard before the curve was factored out.
            if (playerAttTotal <= 0)
            {
                return 1.0;
            }

            var attRatio = enemyAttTotal / playerAttTotal;
            // Within a ±20% band the multiplier is 1; outside it the reward scales quadratically with the
            // ratio, clamped at MaxExpRewardMultiplier so an enemy far above the player can't mint an
            // unbounded payout.
            var expMulti = attRatio is < 0.8 or > 1.2 ? Math.Pow(attRatio, 2) : 1.0;
            return Math.Min(expMulti, ServerGameConstants.MaxExpRewardMultiplier);
        }

        /// <summary>
        /// Floors <paramref name="reward"/> and clamps it into <see cref="int"/> range. <c>EnemyRating</c> is
        /// bounded by authored content (unlike the old unbounded core-attribute sum), but a large authored enemy
        /// times <see cref="ServerGameConstants.XpScaleK"/> can still in principle approach <see cref="int.MaxValue"/>;
        /// clamping before the cast avoids the unchecked overflow that would wrap to a negative reward.
        /// </summary>
        private static int ToIntReward(double reward)
        {
            return (int)Math.Floor(Math.Min(reward, int.MaxValue));
        }

        /// <summary>
        /// Sums the additive amounts of the core attributes in <paramref name="modifiers"/> — the legacy "old"
        /// power measure this class was originally named for. No longer used by this class's own constructor;
        /// retained public and unchanged because the combat-rating calibration report (#1533) compares it against
        /// the incoming <see cref="CombatRating"/> to flag enemies/zones the swap re-prices. Both combatants' power
        /// was measured the same way so the difficulty ratio compared like with like: derived attributes (e.g.
        /// MaxHealth) are excluded because they are computed from the core attributes and never appear as their
        /// own modifier, and multiplicative modifiers are excluded because their amount is a scaling factor, not
        /// a flat point total that can be meaningfully summed.
        /// </summary>
        public static double SumCoreAttributes(IEnumerable<AttributeModifier> modifiers)
        {
            return modifiers
                .Where(mod => mod.Type == EModifierType.Additive && Attribute.IsCore(mod.Attribute))
                .Sum(mod => mod.Amount);
        }
    }
}
