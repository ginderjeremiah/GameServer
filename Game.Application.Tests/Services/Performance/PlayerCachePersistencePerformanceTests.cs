using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.TestInfrastructure.Performance;
using Game.DataAccess.Repositories;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Game.TestInfrastructure.Base;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Game.Application.Tests.Services.Performance
{
    /// <summary>
    /// Full round-trip context for <c>PlayerCacheMappingPerformanceTests</c>' isolated mapping/serialization
    /// numbers (#2340): measures real <see cref="PlayerRepository.SavePlayer"/> calls
    /// (event dispatch + Redis LPUSH + cache write, against real Postgres/Redis containers) for a
    /// late-game-shaped account against a freshly-created one, so the mapping/serialize share of the total
    /// per-save cost can actually be judged rather than guessed at. Observability-first like
    /// <c>BattleRoundTripPerformanceTests</c>: only a generous catastrophic ceiling is asserted, everything
    /// else is logged for tracking.
    /// </summary>
    [Trait("Category", "Performance")]
    [Collection("Integration")]
    public class PlayerCachePersistencePerformanceTests : ApplicationIntegrationTestBase
    {
        private const int WarmupIterations = 3;
        private const int SampleCount = 15;

        private const int LateGameItemCount = 400;
        private const int LateGameSkillCount = 200;

        // Generous catastrophic-regression ceiling, mirroring BattleRoundTripPerformanceTests — real
        // container latency is environment-dependent, so this only catches an order-of-magnitude blow-up.
        private const double SaveCeilingMs = 1000.0;

        private readonly ITestOutputHelper _output;

        public PlayerCachePersistencePerformanceTests(IntegrationTestContainers containers, ITestOutputHelper testOutputHelper)
            : base(containers, testOutputHelper)
        {
            _output = testOutputHelper;
        }

        [Fact]
        public async Task SavePlayer_LateGameAccount_ComparedAgainstFreshAccount()
        {
            var freshPlayerId = await SeedPlayerAsync(itemCount: 0, skillCount: 0);
            var lateGamePlayerId = await SeedPlayerAsync(LateGameItemCount, LateGameSkillCount);

            var freshStats = await MeasureSavePlayerAsync(freshPlayerId);
            var lateGameStats = await MeasureSavePlayerAsync(lateGamePlayerId);

            _output.WriteLine(
                $"SavePlayer, fresh account (0 items/0 skills): {freshStats.MinMicroseconds / 1000.0:F3} ms (min), "
                + $"{freshStats.MedianMicroseconds / 1000.0:F3} ms (median)");
            _output.WriteLine(
                $"SavePlayer, late-game account ({LateGameItemCount} items/{LateGameItemCount / 2} applied "
                + $"mods/{LateGameSkillCount} skills): {lateGameStats.MinMicroseconds / 1000.0:F3} ms (min), "
                + $"{lateGameStats.MedianMicroseconds / 1000.0:F3} ms (median)");
            _output.WriteLine(
                $"Delta attributable to account size: "
                + $"{(lateGameStats.MinMicroseconds - freshStats.MinMicroseconds) / 1000.0:F3} ms (min), "
                + $"{lateGameStats.MinMicroseconds / freshStats.MinMicroseconds:F2}x fresh account's cost — "
                + "compare against PlayerCacheMappingPerformanceTests' isolated ToCacheModel/Serialize figures "
                + "to see what fraction of a save this mapping actually accounts for.");

            var lateGameMs = lateGameStats.MinMicroseconds / 1000.0;
            Assert.True(
                lateGameMs < SaveCeilingMs,
                $"SavePlayer for a {LateGameItemCount}-item late-game account took {lateGameMs:F2} ms (min), "
                + $"exceeding the {SaveCeilingMs:F0} ms catastrophic-regression ceiling.");
        }

        private async Task<MeasurementResult> MeasureSavePlayerAsync(int playerId)
        {
            var scopesToDispose = new List<IServiceScope>();

            var result = await PerformanceMeasurement.MeasureAsync(
                createInput: async () =>
                {
                    var scope = CreateScope();
                    scopesToDispose.Add(scope);
                    var repo = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
                    var player = await repo.GetPlayer(playerId, CancellationToken)
                        ?? throw new InvalidOperationException($"Seeded player {playerId} was not found.");
                    // Guarantees a PlayerCoreUpdatedEvent, so every measured save does the same real
                    // dispatch + publish + cache-write work SavePlayer always does.
                    player.GrantExp(1);
                    return (Repo: repo, Player: player);
                },
                timedOperation: input => input.Repo.SavePlayer(input.Player, CancellationToken),
                warmupIterations: WarmupIterations,
                sampleCount: SampleCount,
                operationsPerSample: 1);

            foreach (var scope in scopesToDispose)
            {
                scope.Dispose();
            }

            return result;
        }

        /// <summary>
        /// Bulk-seeds a player owning <paramref name="itemCount"/> unlocked items (half carrying one applied
        /// mod each) and <paramref name="skillCount"/> unlocked skills, using <c>AddRange</c> + one
        /// <c>SaveChangesAsync</c> per table rather than <see cref="TestDataSeeder"/>'s one-row-per-round-trip
        /// helpers, which would make seeding hundreds of rows the slow part of this test.
        /// </summary>
        private async Task<int> SeedPlayerAsync(int itemCount, int skillCount)
        {
            using var scope = CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            var cls = await TestDataSeeder.CreateClassAsync(context);
            var zone = await TestDataSeeder.CreateZoneAsync(context);
            // Username is varchar(20); a short random suffix keeps the two SeedPlayerAsync calls in a single
            // test from colliding without approaching that limit.
            var user = await TestDataSeeder.CreateUserAsync(context, username: $"perf{Guid.NewGuid():N}"[..20]);
            var player = await TestDataSeeder.CreatePlayerAsync(context, user.Id, zoneId: zone.Id, classId: cls.Id);

            var items = Enumerable.Range(0, itemCount)
                .Select(i => new Item
                {
                    Name = $"Perf Item {i}",
                    Description = "",
                    ItemCategoryId = (int)EItemCategory.Weapon,
                    RarityId = (int)ERarity.Common,
                    IconPath = "",
                    DesignerNotes = "",
                })
                .ToList();
            context.Items.AddRange(items);
            await context.SaveChangesAsync();

            context.ItemAttributes.AddRange(
                items.Select(item => new ItemAttribute { ItemId = item.Id, AttributeId = (int)EAttribute.Strength, Amount = 5m }));
            await context.SaveChangesAsync();

            var appliedModCount = itemCount / 2;
            var mods = Enumerable.Range(0, appliedModCount)
                .Select(i => new ItemMod
                {
                    Name = $"Perf Mod {i}",
                    Description = "",
                    ItemModTypeId = (int)EItemModType.Prefix,
                    RarityId = (int)ERarity.Common,
                    DesignerNotes = "",
                })
                .ToList();
            context.ItemMods.AddRange(mods);
            await context.SaveChangesAsync();

            context.ItemModAttributes.AddRange(
                mods.Select(mod => new ItemModAttribute { ItemModId = mod.Id, AttributeId = (int)EAttribute.Strength, Amount = 5m }));
            await context.SaveChangesAsync();

            var skills = Enumerable.Range(0, skillCount)
                .Select(i => new Skill
                {
                    Name = $"Perf Skill {i}",
                    Description = "",
                    BaseDamage = 10m,
                    CooldownMs = 1000,
                    IconPath = "",
                    RarityId = (int)ERarity.Common,
                    Word = "",
                    Pronunciation = "",
                    Translation = "",
                    Acquisition = (int)ESkillAcquisition.Player,
                    DesignerNotes = "",
                })
                .ToList();
            context.Skills.AddRange(skills);
            await context.SaveChangesAsync();

            context.SkillDamagePortions.AddRange(
                skills.Select(skill => new SkillDamagePortion { SkillId = skill.Id, DamageType = (int)EDamageType.Physical, Weight = 1.0m }));
            context.SkillDamageMultipliers.AddRange(
                skills.Select(skill => new SkillDamageMultiplier { SkillId = skill.Id, AttributeId = (int)EAttribute.Strength, Multiplier = 1.0m }));
            await context.SaveChangesAsync();

            context.UnlockedItems.AddRange(
                items.Select(item => new UnlockedItem { PlayerId = player.Id, ItemId = item.Id, Favorite = false }));
            await context.SaveChangesAsync();

            var modSlots = items.Take(appliedModCount)
                .Select(item => new ItemModSlot { ItemId = item.Id, ItemModSlotTypeId = (int)EItemModType.Prefix })
                .ToList();
            context.ItemModSlots.AddRange(modSlots);
            await context.SaveChangesAsync();

            context.UnlockedMods.AddRange(mods.Select(mod => new UnlockedMod { PlayerId = player.Id, ItemModId = mod.Id }));
            context.AppliedMods.AddRange(
                modSlots.Zip(mods, (slot, mod) => new AppliedMod
                {
                    PlayerId = player.Id,
                    ItemId = slot.ItemId,
                    ItemModSlotId = slot.Id,
                    ItemModId = mod.Id,
                }));
            await context.SaveChangesAsync();

            context.PlayerSkills.AddRange(
                skills.Select((skill, index) => new PlayerSkill
                {
                    PlayerId = player.Id,
                    SkillId = skill.Id,
                    Selected = index < GameConstants.MaxSelectedSkills,
                    Order = index,
                }));
            await context.SaveChangesAsync();

            // Reference data was seeded directly, so the in-memory reference caches need reloading before
            // GetPlayer's rehydration can resolve the new items/mods/skills.
            await ReloadReferenceCachesAsync();

            return player.Id;
        }
    }
}
