using System.Text;
using Game.Core;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;
using Game.Core.TestInfrastructure.Builders;
using Game.Core.TestInfrastructure.Performance;
using Game.DataAccess.Mapping;
using Xunit;

namespace Game.Application.Tests.Mapping
{
    /// <summary>
    /// Measures <see cref="PlayerCacheMapper.ToCacheModel"/> and the JSON serialization
    /// <see cref="Game.DataAccess.Repositories.PlayerRepository.SavePlayer"/> runs it through on every save
    /// (#2340), isolated from the
    /// Redis/dispatch I/O around them (see <c>PlayerCachePersistencePerformanceTests</c> for the full
    /// round trip). "Measure first" is the issue's own suggested first step before picking between its two
    /// proposed directions (a Redis-hash split mirroring #1635, or skipping unchanged sections) — both of
    /// which add real complexity to the hottest, most correctness-sensitive persistence path in the game, so
    /// this establishes whether either is actually warranted.
    /// <para>
    /// A late-game-shaped fixture (hundreds of unlocked items/mods/skills) is compared against an
    /// early-game one at a known ~20x size ratio, mirroring <c>BattlePerformanceTests</c>' scaling-gate
    /// convention: the ratio cancels out machine speed, so what is actually asserted is that the cost
    /// scales roughly <em>linearly</em> with account size (catching an accidental O(n²) in the mapping),
    /// not any absolute number. Absolute per-save figures are logged, not gated — see
    /// <c>PlayerCachePersistencePerformanceTests</c> for the full-round-trip context they need to be
    /// judged against.
    /// </para>
    /// </summary>
    [Trait("Category", "Performance")]
    public class PlayerCacheMappingPerformanceTests
    {
        private const int WarmupIterations = 10;
        private const int SampleCount = 20;
        private const int OperationsPerSample = 50;

        // ~20x growth in every owned-reference collection between the two fixtures, so the scaling ratio
        // is a clean, machine-independent signal (see class remarks).
        private const int EarlyGameItemCount = 20;
        private const int EarlyGameSkillCount = 10;
        private const int LateGameItemCount = 400;
        private const int LateGameSkillCount = 200;

        // Half of every fixture's unlocked items carry one applied mod, mirroring a typical build where
        // roughly half of a hoarded inventory is actually socketed.
        private const double AppliedModFraction = 0.5;

        // Generous linear-scaling budget (see BattlePerformanceTests.LinearScalingTolerance for the same
        // pattern): a genuine O(n) mapping lands near the 20x size ratio; this catches an accidental
        // super-linear regression (e.g. an O(n²) lookup creeping into the per-item loop) without flaking
        // on ordinary CI noise.
        private const double LinearScalingTolerance = 2.0;

        private readonly ITestOutputHelper _output;

        public PlayerCacheMappingPerformanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ToCacheModel_ScalesLinearlyWithAccountSize()
        {
            var earlyGame = BuildPlayer(EarlyGameItemCount, EarlyGameSkillCount);
            var lateGame = BuildPlayer(LateGameItemCount, LateGameSkillCount);

            var earlyMapMicros = MeasureMapOnly(earlyGame);
            var lateMapMicros = MeasureMapOnly(lateGame);

            var sizeRatio = (double)LateGameItemCount / EarlyGameItemCount;
            var mapRatio = lateMapMicros / earlyMapMicros;
            var threshold = sizeRatio * LinearScalingTolerance;

            _output.WriteLine(
                $"ToCacheModel: {earlyMapMicros:F2} us ({EarlyGameItemCount} items/{EarlyGameSkillCount} skills) "
                + $"-> {lateMapMicros:F2} us ({LateGameItemCount} items/{LateGameSkillCount} skills); "
                + $"{mapRatio:F2}x over a {sizeRatio:F0}x size ratio (budget {threshold:F1}x)");

            Assert.True(
                mapRatio <= threshold,
                $"ToCacheModel cost scaled {mapRatio:F2}x for a {sizeRatio:F0}x larger account, exceeding the "
                + $"{threshold:F1}x linear-scaling budget — points at super-linear cost growth in the mapping.");
        }

        [Fact]
        public void ToCacheModelAndSerialize_LogsAbsoluteCostAndPayloadSize()
        {
            var lateGame = BuildPlayer(LateGameItemCount, LateGameSkillCount);

            var mapOnlyMicros = MeasureMapOnly(lateGame);
            var mapAndSerializeMicros = MeasureMapAndSerialize(lateGame);
            var payloadBytes = Encoding.UTF8.GetByteCount(PlayerCacheMapper.ToCacheModel(lateGame).Serialize());

            _output.WriteLine(
                $"Late-game player ({LateGameItemCount} items, {LateGameItemCount * AppliedModFraction:F0} applied "
                + $"mods, {LateGameSkillCount} skills):");
            _output.WriteLine($"  ToCacheModel only:        {mapOnlyMicros:F2} us (min)");
            _output.WriteLine($"  ToCacheModel + Serialize: {mapAndSerializeMicros:F2} us (min)");
            _output.WriteLine($"  Serialized payload size:  {payloadBytes:N0} bytes");

            // Not a tight gate — this is the "measure first" step #2340 asks for, so the figures above are
            // the deliverable. Only a coarse catastrophic-regression ceiling is asserted, mirroring
            // BattlePerformanceTests.MaxLengthRealisticCeilingMs: this operation is currently well under a
            // millisecond even at this size, so anything reaching 50ms points at a real regression, not noise.
            const double CatastrophicCeilingMicros = 50_000.0;
            Assert.True(
                mapAndSerializeMicros < CatastrophicCeilingMicros,
                $"ToCacheModel + Serialize took {mapAndSerializeMicros:F2} us for a {LateGameItemCount}-item "
                + $"account, exceeding the {CatastrophicCeilingMicros:F0} us catastrophic-regression ceiling.");
        }

        private static double MeasureMapOnly(Player player)
        {
            // ToCacheModel is a pure read of the aggregate (builds new objects, mutates nothing), so the
            // same player instance is safe to reuse as every operation's input.
            var result = PerformanceMeasurement.Measure(
                createInput: () => player,
                timedOperation: p => PlayerCacheMapper.ToCacheModel(p),
                warmupIterations: WarmupIterations,
                sampleCount: SampleCount,
                operationsPerSample: OperationsPerSample);

            return result.MinMicroseconds;
        }

        private static double MeasureMapAndSerialize(Player player)
        {
            var result = PerformanceMeasurement.Measure(
                createInput: () => player,
                timedOperation: p => PlayerCacheMapper.ToCacheModel(p).Serialize(),
                warmupIterations: WarmupIterations,
                sampleCount: SampleCount,
                operationsPerSample: OperationsPerSample);

            return result.MinMicroseconds;
        }

        /// <summary>
        /// Builds a synthetic player with <paramref name="itemCount"/> unlocked items (half carrying one
        /// applied mod each, per <see cref="AppliedModFraction"/>), all six equipment slots filled, and
        /// <paramref name="skillCount"/> unlocked skills (a full loadout selected) — the shape
        /// <see cref="PlayerCacheMapper.ToCacheModel"/> actually walks on every save.
        /// </summary>
        private static Player BuildPlayer(int itemCount, int skillCount)
        {
            var inventory = new Inventory();

            var appliedModCount = (int)(itemCount * AppliedModFraction);
            var unlockedItems = new List<UnlockedItemSlot>(itemCount);
            var unlockedModIds = new HashSet<int>();

            for (var i = 0; i < itemCount; i++)
            {
                var item = BuildItem(i);
                var appliedMods = new List<AppliedModSlot>();

                if (i < appliedModCount)
                {
                    var mod = BuildItemMod(i);
                    unlockedModIds.Add(mod.Id);
                    appliedMods.Add(new AppliedModSlot
                    {
                        ItemModId = mod.Id,
                        ItemModSlotId = i,
                        ItemMod = mod,
                    });
                }

                unlockedItems.Add(new UnlockedItemSlot
                {
                    Item = item,
                    AppliedMods = appliedMods,
                    Favorite = i % 10 == 0,
                });
            }

            inventory.UnlockedItems = unlockedItems;
            inventory.UnlockedMods = unlockedModIds;

            // Fill every equipment slot so the equipped-item lookup the mapper builds up front is at
            // realistic (full) occupancy.
            foreach (var slot in inventory.EquipmentSlots)
            {
                var equippableItem = unlockedItems.First(u => u.Item.Category == slot.ItemCategory).Item;
                slot.Set(equippableItem);
            }

            var skills = new List<Skill>(skillCount);
            for (var i = 0; i < skillCount; i++)
            {
                skills.Add(BuildSkill(i));
            }

            var selectedSkills = skills.Take(GameConstants.MaxSelectedSkills).ToList();

            return new PlayerBuilder()
                .WithInventory(inventory)
                .WithSkills(skills)
                .WithSelectedSkills(selectedSkills)
                .Build();
        }

        // Items round-robin across every equippable category so BuildPlayer can fill all six equipment slots.
        private static readonly EItemCategory[] EquippableCategories =
        [
            EItemCategory.Helm, EItemCategory.Chest, EItemCategory.Leg,
            EItemCategory.Boot, EItemCategory.Weapon, EItemCategory.Accessory,
        ];

        private static Item BuildItem(int id) => new()
        {
            Id = id,
            Name = $"Item {id}",
            Description = string.Empty,
            Category = EquippableCategories[id % EquippableCategories.Length],
            Rarity = ERarity.Common,
            Attributes = [],
            ModSlots = [],
        };

        private static ItemMod BuildItemMod(int id) => new()
        {
            Id = id,
            Name = $"Mod {id}",
            Description = string.Empty,
            Type = EItemModType.Prefix,
            Rarity = ERarity.Common,
            Attributes = [],
        };

        private static Skill BuildSkill(int id) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            Description = string.Empty,
            DamagePortions = [new SkillDamagePortion { Type = EDamageType.Physical, Weight = 1.0 }],
            CooldownMs = 1000,
            BaseDamage = 10,
            CriticalChance = 0,
            DamageMultipliers = [],
            Effects = [],
        };
    }
}
