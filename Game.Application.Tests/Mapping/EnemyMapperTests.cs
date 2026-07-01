using Game.Core;
using Game.DataAccess.Mapping;
using Xunit;
using Entities = Game.Infrastructure.Entities;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="EnemyMapper.ToContract"/>: the scalar fields and the three child collections
    /// (attribute distributions, the skill pool projected from <c>EnemySkills</c>, and the spawn table
    /// projected from <c>ZoneEnemies</c>) round-trip from the entity to the client-visible reference-data
    /// contract, which drives the enemies set's version hash.
    /// </summary>
    public class EnemyMapperTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ToContract_RoundTripsScalarFields(bool isBoss)
        {
            var retiredAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var entity = NewEnemy(isBoss: isBoss, retiredAt: retiredAt);

            var contract = EnemyMapper.ToContract(entity);

            Assert.Equal(0, contract.Id);
            Assert.Equal("Goblin", contract.Name);
            Assert.Equal(isBoss, contract.IsBoss);
            // Authoring-only metadata rides the contract only; the lean Core enemy model has no such field.
            Assert.Equal("designer intent", contract.DesignerNotes);
            Assert.Equal(retiredAt, contract.RetiredAt);
        }

        [Fact]
        public void ToContract_PreservesNullRetiredAt()
        {
            var contract = EnemyMapper.ToContract(NewEnemy());

            Assert.Null(contract.RetiredAt);
        }

        [Fact]
        public void ToContract_MapsChildCollections()
        {
            var entity = NewEnemy(
                distributions: [(EAttribute.Strength, 5m, 1m), (EAttribute.Endurance, 3m, 2m)],
                skillPool: [3, 7],
                spawns: [(ZoneId: 1, Weight: 10), (ZoneId: 4, Weight: 20)]);

            var contract = EnemyMapper.ToContract(entity);

            Assert.Collection(contract.AttributeDistribution,
                d => { Assert.Equal(EAttribute.Strength, d.AttributeId); Assert.Equal(5m, d.BaseAmount); Assert.Equal(1m, d.AmountPerLevel); },
                d => { Assert.Equal(EAttribute.Endurance, d.AttributeId); Assert.Equal(3m, d.BaseAmount); Assert.Equal(2m, d.AmountPerLevel); });
            Assert.Equal([3, 7], contract.SkillPool);
            Assert.Collection(contract.Spawns,
                s => { Assert.Equal(1, s.ZoneId); Assert.Equal(10, s.Weight); },
                s => { Assert.Equal(4, s.ZoneId); Assert.Equal(20, s.Weight); });
        }

        private static Entities.Enemy NewEnemy(
            bool isBoss = false,
            DateTime? retiredAt = null,
            List<(EAttribute Attribute, decimal Base, decimal PerLevel)>? distributions = null,
            List<int>? skillPool = null,
            List<(int ZoneId, int Weight)>? spawns = null) => new()
            {
                Id = 0,
                Name = "Goblin",
                IsBoss = isBoss,
                DesignerNotes = "designer intent",
                RetiredAt = retiredAt,
                AttributeDistributions = (distributions ?? []).Select(d => new Entities.AttributeDistribution
                {
                    EnemyId = 0,
                    AttributeId = (int)d.Attribute,
                    BaseAmount = d.Base,
                    AmountPerLevel = d.PerLevel,
                }).ToList(),
                EnemySkills = (skillPool ?? []).Select(id => new Entities.EnemySkill { EnemyId = 0, SkillId = id }).ToList(),
                ZoneEnemies = (spawns ?? []).Select(s => new Entities.ZoneEnemy
                {
                    ZoneId = s.ZoneId,
                    EnemyId = 0,
                    Weight = s.Weight,
                }).ToList(),
            };
    }
}
