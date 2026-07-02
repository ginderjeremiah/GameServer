using Game.Core;
using Game.Core.Attributes;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Xunit;
using static Game.Core.EAttribute;

namespace Game.Core.Tests.Enemies
{
    /// <summary>
    /// End-to-end verification that enemies are first-class participants in the typed-damage system
    /// (spike #1320, area D). Unlike <c>BattleContextTests</c>, which exercise the <see cref="BattleContext.DamageTarget"/>
    /// primitive against ad-hoc battlers, these build a real <see cref="Enemy"/> from authored
    /// <see cref="AttributeDistribution"/> rows and resolve it to a battler through the exact production path
    /// (<c>new Battler(new AttributeCollection(enemy.GetAttributeModifiers()), enemy.BattleSkills, enemy.Level)</c>),
    /// so the authored resistance/amplification distributions, their level-scaling, and the automatic typing of
    /// the enemy's fielded skills are all covered with no battle-pipeline change.
    /// Every enemy carries an <c>(Endurance, 0, 0)</c> row so its Toughness is 0 — type resistance is then the
    /// only mitigation, matching the convention in <c>BattleContextTests</c>.
    /// </summary>
    public class TypedEnemyCombatTests
    {
        // ── Enemy as defender: authored resistance distributions ─────────────

        [Fact]
        public void FireResistantEnemy_TakesReducedFireDamage_AndPhysicalIsUnaffected()
        {
            // FireResistance 0.5 halves a Fire hit but leaves a Physical hit untouched — proving the reduction
            // is type-routed through the applies() map, not a flat damage cut.
            var enemy = EnemyBattler(MakeEnemy(level: 1, (Endurance, 0, 0), (FireResistance, 0.5m, 0m)));
            var context = new BattleContext(Player(), enemy, timeDelta: 0, new Mulberry32(0));

            var fire = context.DamageTarget(40, Single(EDamageType.Fire), 0);
            var physical = context.DamageTarget(40, Single(EDamageType.Physical), 0);

            Assert.Equal(20, fire, 0.001);     // 40 × (1 − 0.5)
            Assert.Equal(40, physical, 0.001); // unresisted
        }

        [Fact]
        public void FireVulnerableEnemy_TakesExtraFireDamage()
        {
            // Negative resistance is vulnerability: 40 × (1 − (−0.5)) = 60.
            var enemy = EnemyBattler(MakeEnemy(level: 1, (Endurance, 0, 0), (FireResistance, -0.5m, 0m)));
            var context = new BattleContext(Player(), enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Equal(60, dealt, 0.001);
        }

        [Fact]
        public void FireImmuneEnemy_TakesNoFireDamage()
        {
            // FireResistance 1.0 is full immunity: 40 × (1 − 1) = 0.
            var enemy = EnemyBattler(MakeEnemy(level: 1, (Endurance, 0, 0), (FireResistance, 1.0m, 0m)));
            var context = new BattleContext(Player(), enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Equal(0, dealt, 0.001);
        }

        [Fact]
        public void FireAbsorbingEnemy_HealsFromFireDamage()
        {
            // FireResistance above 1 drives the post-resistance hit negative — a net heal (the enemy "absorbs"
            // fire). A physical hit first opens 30 of healing room; the absorbed Fire hit then heals it.
            var enemy = EnemyBattler(MakeEnemy(level: 1, (Endurance, 0, 0), (FireResistance, 2.0m, 0m)));
            var context = new BattleContext(Player(), enemy, timeDelta: 0, new Mulberry32(0));

            context.DamageTarget(30, Single(EDamageType.Physical), 0); // open 30 of room
            var healthBeforeAbsorb = enemy.CurrentHealth;
            var net = context.DamageTarget(20, Single(EDamageType.Fire), 0); // 20 × (1 − 2) = −20, absorbed

            Assert.Equal(-20, net, 0.001);
            Assert.Equal(healthBeforeAbsorb + 20, enemy.CurrentHealth, 0.001);
        }

        [Fact]
        public void ElementalResistantEnemy_ReducesFireDamage_ViaSharedCategoryKey()
        {
            // A cross-cutting category resistance reduces every leaf it covers: ElementalResistance 0.5 halves a
            // Fire hit (applies(Fire) = [Fire, Elemental]), with no per-fire resistance authored.
            var enemy = EnemyBattler(MakeEnemy(level: 1, (Endurance, 0, 0), (ElementalResistance, 0.5m, 0m)));
            var context = new BattleContext(Player(), enemy, timeDelta: 0, new Mulberry32(0));

            var dealt = context.DamageTarget(40, Single(EDamageType.Fire), 0);

            Assert.Equal(20, dealt, 0.001);
        }

        [Fact]
        public void ResistanceDistribution_ScalesWithEnemyLevel()
        {
            // The resistance is authored as a distribution (BaseAmount + AmountPerLevel × level), so it resolves
            // at the enemy's level: 0.1/level gives 0.2 resistance at level 2 and 0.8 at level 8 — a higher-level
            // enemy of the same template resists more.
            var lowLevel = EnemyBattler(MakeEnemy(level: 2, (Endurance, 0, 0), (FireResistance, 0m, 0.1m)));
            var highLevel = EnemyBattler(MakeEnemy(level: 8, (Endurance, 0, 0), (FireResistance, 0m, 0.1m)));

            var lowDealt = new BattleContext(Player(), lowLevel, timeDelta: 0, new Mulberry32(0))
                .DamageTarget(100, Single(EDamageType.Fire), 0);
            var highDealt = new BattleContext(Player(), highLevel, timeDelta: 0, new Mulberry32(0))
                .DamageTarget(100, Single(EDamageType.Fire), 0);

            Assert.Equal(80, lowDealt, 0.001);  // 100 × (1 − 0.2)
            Assert.Equal(20, highDealt, 0.001); // 100 × (1 − 0.8)
        }

        // ── Enemy as attacker: typed skills come free ────────────────────────

        [Fact]
        public void EnemyFieldsTypedSkill_DealsResistibleTypedDamage_ToPlayer()
        {
            // Enemies field the same Skill instances as players, so an enemy's Fire skill is typed automatically:
            // its DamagePortions flow through DamageTarget unchanged, and the player's FireResistance resists it.
            var enemy = EnemyBattler(MakeEnemy(level: 1, FireSkill(baseDamage: 20), (Endurance, 0, 0)));
            var player = Player((FireResistance, 0.5));
            var context = new BattleContext(player, enemy, timeDelta: 100, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers(); // the enemy attacks the player
            var playerHealthBefore = player.CurrentHealth;

            enemy.Update(context);

            Assert.Equal(10, context.Stats.PlayerDamageTaken, 0.001);     // 20 × (1 − 0.5)
            Assert.Equal(20, Exposure(context, EDamageType.Fire), 0.001); // pre-mitigation
            Assert.Equal(playerHealthBefore - 10, player.CurrentHealth, 0.001);
        }

        [Fact]
        public void EnemyAmplificationDistribution_IncreasesItsOutgoingTypedDamage()
        {
            // Amplification distributions are authored on enemies the same way resistances are. FireAmplification
            // 0.5 raises the enemy's outgoing Fire hit by 50% (20 → 30) against an unresistant player.
            var enemy = EnemyBattler(MakeEnemy(level: 1, FireSkill(baseDamage: 20), (Endurance, 0, 0), (FireAmplification, 0.5m, 0m)));
            var player = Player();
            var context = new BattleContext(player, enemy, timeDelta: 100, new Mulberry32(0));
            context.SwapActiveAndTargetBattlers();

            enemy.Update(context);

            Assert.Equal(30, context.Stats.PlayerDamageTaken, 0.001); // 20 × (1 + 0.5)
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static Battler EnemyBattler(Enemy enemy)
        {
            // The full authored loadout, deterministically (no shuffle/cap), mirroring the production
            // resolve-then-build path.
            enemy.SelectAllBattleSkills();
            return new Battler(new AttributeCollection(enemy.GetAttributeModifiers()), enemy.BattleSkills, enemy.Level);
        }

        private static Enemy MakeEnemy(int level, params (EAttribute Attribute, decimal BaseAmount, decimal AmountPerLevel)[] distributions) =>
            MakeEnemy(level, [], distributions);

        private static Enemy MakeEnemy(int level, IReadOnlyList<Skill> skills, params (EAttribute Attribute, decimal BaseAmount, decimal AmountPerLevel)[] distributions) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            Level = level,
            AttributeDistributions = distributions
                .Select(d => new AttributeDistribution { AttributeId = d.Attribute, BaseAmount = d.BaseAmount, AmountPerLevel = d.AmountPerLevel })
                .ToList(),
            AvailableSkills = skills,
        };

        private static IReadOnlyList<Skill> FireSkill(double baseDamage) =>
            [new Skill
            {
                Id = 0,
                Name = "Enemy Fireball",
                Description = "",
                BaseDamage = baseDamage,
                CriticalChance = 0,
                CooldownMs = 0, // fires on the first tick
                DamagePortions = [new SkillDamagePortion { Type = EDamageType.Fire, Weight = 1.0 }],
                DamageMultipliers = [],
                Effects = [],
            }];

        private static Battler Player(params (EAttribute Attribute, double Amount)[] attributes)
        {
            var allocations = attributes
                .Select(a => new StatAllocation { Attribute = a.Attribute, Amount = a.Amount })
                .ToList();
            var player = new PlayerBuilder().WithStatAllocations(allocations).Build();
            return BattlerFactory.FromPlayer(player);
        }

        private static IReadOnlyList<SkillDamagePortion> Single(EDamageType type) =>
            [new SkillDamagePortion { Type = type, Weight = 1.0 }];

        private static double Exposure(BattleContext context, EDamageType type) =>
            context.Stats.TypedDamageExposure.TryGetValue(type, out var value) ? value : 0;
    }
}
