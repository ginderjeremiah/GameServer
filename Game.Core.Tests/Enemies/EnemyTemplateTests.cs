using Game.Core.Attributes;
using Game.Core.Enemies;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Enemies
{
    public class EnemyTemplateTests
    {
        [Fact]
        public void ToEnemy_CopiesIdentityAndAppliesLevel()
        {
            var template = MakeTemplate(Skills(3));

            var enemy = template.ToEnemy(level: 7);

            Assert.Equal(template.Id, enemy.Id);
            Assert.Equal(template.Name, enemy.Name);
            Assert.Equal(template.IsBoss, enemy.IsBoss);
            Assert.Equal(7, enemy.Level);
        }

        [Fact]
        public void ToEnemy_ReusesTemplateBuildingBlocksByReference()
        {
            // The optimization (#584): producing an enemy clones a pre-mapped template rather than re-mapping
            // the skill/attribute graph, so the shared blocks are handed out by reference, not rebuilt.
            var template = MakeTemplate(Skills(4));

            var first = template.ToEnemy(level: 1);
            var second = template.ToEnemy(level: 9);

            Assert.Same(template.AvailableSkills, first.AvailableSkills);
            Assert.Same(template.AttributeDistributions, first.AttributeDistributions);
            Assert.Same(first.AvailableSkills, second.AvailableSkills);
            Assert.Same(first.AttributeDistributions, second.AttributeDistributions);
        }

        [Fact]
        public void ToEnemy_ProducesDistinctInstancesPerCall()
        {
            // Each encounter needs its own instance: the level differs and the battle-skill selection is
            // mutable per encounter, so the template can't hand out one shared Enemy.
            var template = MakeTemplate(Skills(4));

            var first = template.ToEnemy(level: 3);
            var second = template.ToEnemy(level: 3);

            Assert.NotSame(first, second);
        }

        [Fact]
        public void ToEnemy_SelectingBattleSkillsOnOneCloneDoesNotAffectAnother()
        {
            var template = MakeTemplate(Skills(6));

            var encounter = template.ToEnemy(level: 1);
            var other = template.ToEnemy(level: 1);
            encounter.SelectBattleSkills();

            // The selection lives on the produced instance, not the template, so a sibling clone is unaffected.
            Assert.Equal(4, encounter.BattleSkills.Count);
            Assert.Throws<InvalidOperationException>(() => _ = other.BattleSkills);
        }

        [Fact]
        public void ToEnemy_ProducedEnemyDrawsBattleSkillsFromTemplateLoadout()
        {
            var available = Skills(8);
            var availableIds = available.Select(s => s.Id).ToHashSet();
            var template = MakeTemplate(available);

            var enemy = template.ToEnemy(level: 1);
            enemy.SelectBattleSkills();

            Assert.All(enemy.BattleSkills, s => Assert.Contains(s.Id, availableIds));
        }

        private static List<Skill> Skills(int count) =>
            [.. Enumerable.Range(0, count).Select(MakeSkill)];

        private static Skill MakeSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            Description = "",
            Rarity = ERarity.Common,
            DamageType = EDamageType.Physical,
            BaseDamage = 1,
            CooldownMs = 1000,
            DamageMultipliers = [],
            Effects = [],
        };

        private static EnemyTemplate MakeTemplate(List<Skill> skills) => new()
        {
            Id = 1,
            Name = "Test Enemy",
            IsBoss = false,
            AttributeDistributions =
            [
                new AttributeDistribution { AttributeId = EAttribute.Strength, BaseAmount = 5, AmountPerLevel = 2 },
            ],
            AvailableSkills = skills,
        };
    }
}
