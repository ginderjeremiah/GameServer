using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Skills;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Battle
{
    /// <summary>
    /// The enemy-authored bounty curve (spike #1526 Decision 4): <c>ExpReward = floor(k × EnemyRating ×
    /// min(EnemyRating ÷ PlayerRating, 1)²)</c>, both ratings from <see cref="CombatRating.Rate"/> (unit-tested
    /// independently in <c>CombatRatingTests</c>). These pin the wiring — that <see cref="DefeatRewards"/>
    /// correctly composes the two ratings into the curve — and the documented properties: a matched-or-stronger
    /// enemy saturates the bounty with no upward premium (punching up no longer pays extra — sandbagging dies
    /// structurally), a trivial enemy pays quadratically less (anti-grind), and the int-range overflow clamp.
    /// Attribute-composition correctness (locked base, signature passive, equipment) is <see cref="Battler"/>/
    /// <c>BattleSnapshot</c>'s concern, not this class's — those are covered by their own test suites.
    /// </summary>
    public class DefeatRewardsTests
    {
        [Fact]
        public void ExpReward_MatchedRatings_PaysTheFullBountyWithMultiplierOne()
        {
            var (playerBattler, enemy) = MakeMatchup(playerEndurance: 20, enemyEndurance: 20);

            var rewards = new DefeatRewards(playerBattler, enemy);

            // Identical kit + identical Endurance, and no crit/dodge/parry-enabling attributes authored, so the
            // player/enemy asymmetry contributes nothing — the two ratings are numerically equal.
            Assert.Equal(rewards.EnemyRating, rewards.PlayerRating, precision: 9);
            Assert.Equal(1.0, rewards.DifficultyMultiplier, precision: 9);
            Assert.Equal(ExpectedReward(rewards.EnemyRating, rewards.PlayerRating), rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_StrongEnemy_SaturatesAtTheBounty_NoUpwardPremium()
        {
            // An enemy well above the player's rating clamps the ratio at 1 rather than paying a premium for
            // punching up (spike #1526 Decision 4 — the old upward ratio² premium and its cap both dissolve into
            // this one continuous, self-capping curve).
            var (matchedPlayer, matchedEnemy) = MakeMatchup(playerEndurance: 10, enemyEndurance: 10);
            var (samePlayer, strongerEnemy) = MakeMatchup(playerEndurance: 10, enemyEndurance: 200);

            var matched = new DefeatRewards(matchedPlayer, matchedEnemy);
            var overpowering = new DefeatRewards(samePlayer, strongerEnemy);

            Assert.True(overpowering.EnemyRating > matched.EnemyRating, "the stronger enemy must actually rate higher");
            Assert.Equal(1.0, matched.DifficultyMultiplier, precision: 9);
            Assert.Equal(1.0, overpowering.DifficultyMultiplier, precision: 9);
            Assert.Equal(ExpectedReward(overpowering.EnemyRating, overpowering.PlayerRating), overpowering.ExpReward);
        }

        [Fact]
        public void ExpReward_WeakEnemy_QuadraticallyReduced_AntiGrind()
        {
            var (playerBattler, weakEnemy) = MakeMatchup(playerEndurance: 200, enemyEndurance: 10);

            var rewards = new DefeatRewards(playerBattler, weakEnemy);

            var ratio = rewards.EnemyRating / rewards.PlayerRating;
            Assert.True(ratio < 1.0, "the weak enemy must actually rate lower than the player");
            Assert.Equal(Math.Pow(ratio, 2), rewards.DifficultyMultiplier, precision: 9);
            Assert.Equal(ExpectedReward(rewards.EnemyRating, rewards.PlayerRating), rewards.ExpReward);
        }

        [Fact]
        public void DifficultyMultiplier_WeakerEnemyThanEndurance_MatchesTheClosedForm()
        {
            // Cross-checks the DefeatRewards wiring against the documented formula for an arbitrary (non-1,
            // non-saturated) ratio, rather than only the boundary cases above.
            var (playerBattler, enemy) = MakeMatchup(playerEndurance: 80, enemyEndurance: 30);

            var rewards = new DefeatRewards(playerBattler, enemy);
            var expectedRatio = CombatRating.Rate(enemy.ToBattler(), isPlayer: false)
                / CombatRating.Rate(playerBattler, isPlayer: true);

            Assert.Equal(Math.Pow(Math.Min(expectedRatio, 1.0), 2), rewards.DifficultyMultiplier, precision: 9);
        }

        [Fact]
        public void PlayerRating_EqualsCombatRatingOfThePlayerBattler()
        {
            var (playerBattler, enemy) = MakeMatchup(playerEndurance: 45, enemyEndurance: 15);

            var rewards = new DefeatRewards(playerBattler, enemy);

            Assert.Equal(CombatRating.Rate(playerBattler, isPlayer: true), rewards.PlayerRating, precision: 9);
        }

        [Fact]
        public void EnemyRating_EqualsCombatRatingOfTheEnemysFieldedLoadout()
        {
            var (playerBattler, enemy) = MakeMatchup(playerEndurance: 45, enemyEndurance: 15);

            var rewards = new DefeatRewards(playerBattler, enemy);

            Assert.Equal(CombatRating.Rate(enemy.ToBattler(), isPlayer: false), rewards.EnemyRating, precision: 9);
        }

        [Fact]
        public void ExpReward_EnormousEnemyRating_ClampsToIntMaxValueInsteadOfOverflowing()
        {
            // An absurdly tanky authored enemy (author-controlled Endurance) can push k × EnemyRating past
            // int.MaxValue; the unclamped (int) cast would wrap to a negative value that GrantExp floors to 0,
            // silently zeroing a legitimate reward. The reward must clamp to int.MaxValue instead.
            var (playerBattler, enemy) = MakeMatchup(playerEndurance: 10, enemyEndurance: 1_000_000_000_000_000_000d);

            var rewards = new DefeatRewards(playerBattler, enemy);

            Assert.Equal(int.MaxValue, rewards.ExpReward);
        }

        [Fact]
        public void ExpReward_BareBattlers_ProducesAFiniteNonNegativeReward()
        {
            // No skills, no attributes on either side — CombatRating.Rate's own degenerate-guard floor keeps
            // both ratings strictly positive, so the curve never divides by zero or produces a NaN/negative
            // reward even for a maximally degenerate matchup.
            var playerBattler = new Battler(new AttributeCollection([]), [], 1);
            var enemy = MakeEnemy([], []);
            enemy.SelectAllBattleSkills();

            var rewards = new DefeatRewards(playerBattler, enemy);

            Assert.True(rewards.ExpReward >= 0);
            Assert.False(double.IsNaN(rewards.DifficultyMultiplier));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Builds a player battler and an enemy that field the identical single attack skill, differing only in
        // Endurance (which drives Survivability and therefore the rating) — a controllable way to place the two
        // ratings on either side of (or exactly at) a matched fight without hand-deriving CombatRating's closed
        // form. Neither side authors any crit/dodge/parry-enabling attribute, so the player/enemy asymmetry
        // CombatRating gates on contributes nothing and the two sides are directly comparable.
        private static (Battler PlayerBattler, Enemy Enemy) MakeMatchup(double playerEndurance, double enemyEndurance)
        {
            var skill = MakeSkill();

            var playerBattler = new Battler(
                new AttributeCollection([EnduranceModifier(playerEndurance)]), [skill], 1);

            var enemy = MakeEnemy([(Endurance, enemyEndurance)], [skill]);
            enemy.SelectAllBattleSkills();

            return (playerBattler, enemy);
        }

        private static double ExpectedReward(double enemyRating, double playerRating)
        {
            var ratio = enemyRating / playerRating;
            var multiplier = Math.Pow(Math.Min(ratio, 1.0), 2);
            return Math.Floor(Math.Min(ServerGameConstants.XpScaleK * enemyRating * multiplier, int.MaxValue));
        }

        private static AttributeModifier EnduranceModifier(double amount) => new()
        {
            Attribute = Endurance,
            Amount = amount,
            Type = EModifierType.Additive,
            Source = EAttributeModifierSource.AttributeDistribution,
        };

        private static Skill MakeSkill() => new()
        {
            Id = 1,
            Name = "Test Skill",
            Description = string.Empty,
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
            CooldownMs = 1000,
            BaseDamage = 10,
            CriticalChance = 0,
            DamageMultipliers = [],
            Effects = [],
        };

        private static Enemy MakeEnemy(
            (EAttribute Attribute, double Amount)[] attributes, IReadOnlyList<Skill> availableSkills) => new()
            {
                Id = 1,
                Name = "Test Enemy",
                IsBoss = false,
                Level = 1,
                AttributeDistributions = [.. attributes.Select(a => new AttributeDistribution
            {
                AttributeId = a.Attribute,
                BaseAmount = (decimal)a.Amount,
                AmountPerLevel = 0,
            })],
                AvailableSkills = availableSkills,
            };
    }
}
