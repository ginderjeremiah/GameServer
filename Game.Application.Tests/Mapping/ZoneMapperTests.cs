using Game.DataAccess.Mapping;
using Xunit;
using EntityZone = Game.Infrastructure.Entities.Zone;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Coverage for <see cref="ZoneMapper"/>: the full scalar set (including the nullable boss/unlock FKs and
    /// the <c>IsHome</c> orchestration flag) round-trips to the client-visible <see cref="ZoneMapper.ToContract"/>
    /// contract that drives the zones set's version hash, while <see cref="ZoneMapper.ToCore"/> projects only the
    /// lean encounter-relevant subset — the display-only and orchestration fields are structurally absent from
    /// the battle domain model.
    /// </summary>
    public class ZoneMapperTests
    {
        [Fact]
        public void ToContract_RoundTripsAllScalarFields()
        {
            var retiredAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var entity = NewZone(bossEnemyId: 4, unlockChallengeId: 2, isHome: false, retiredAt: retiredAt);

            var contract = ZoneMapper.ToContract(entity);

            Assert.Equal(0, contract.Id);
            Assert.Equal("Meadow", contract.Name);
            Assert.Equal("A gentle field.", contract.Description);
            Assert.Equal(1, contract.Order);
            Assert.Equal(3, contract.LevelMin);
            Assert.Equal(9, contract.LevelMax);
            Assert.Equal(4, contract.BossEnemyId);
            Assert.Equal(7, contract.BossLevel);
            Assert.Equal(2, contract.UnlockChallengeId);
            Assert.False(contract.IsHome);
            Assert.Equal(retiredAt, contract.RetiredAt);
        }

        [Fact]
        public void ToContract_PreservesNullOptionalFieldsAndHomeFlag()
        {
            var entity = NewZone(bossEnemyId: null, unlockChallengeId: null, isHome: true);

            var contract = ZoneMapper.ToContract(entity);

            Assert.Null(contract.BossEnemyId);
            Assert.Null(contract.UnlockChallengeId);
            Assert.Null(contract.RetiredAt);
            Assert.True(contract.IsHome);
        }

        [Fact]
        public void ToCore_MapsLeanEncounterSubset()
        {
            var entity = NewZone(bossEnemyId: 4, unlockChallengeId: 2);

            var core = ZoneMapper.ToCore(entity);

            Assert.Equal(0, core.Id);
            Assert.Equal(3, core.LevelMin);
            Assert.Equal(9, core.LevelMax);
            Assert.Equal(4, core.BossEnemyId);
            Assert.Equal(7, core.BossLevel);
            Assert.Equal(2, core.UnlockChallengeId);
        }

        [Fact]
        public void ToCore_PreservesNullOptionalFields()
        {
            var core = ZoneMapper.ToCore(NewZone(bossEnemyId: null, unlockChallengeId: null));

            Assert.Null(core.BossEnemyId);
            Assert.Null(core.UnlockChallengeId);
        }

        private static EntityZone NewZone(
            int? bossEnemyId = null,
            int? unlockChallengeId = null,
            bool isHome = false,
            DateTime? retiredAt = null) => new()
            {
                Id = 0,
                Name = "Meadow",
                Description = "A gentle field.",
                Order = 1,
                LevelMin = 3,
                LevelMax = 9,
                BossEnemyId = bossEnemyId,
                BossLevel = 7,
                UnlockChallengeId = unlockChallengeId,
                IsHome = isHome,
                RetiredAt = retiredAt,
                ZoneEnemies = [],
            };
    }
}
