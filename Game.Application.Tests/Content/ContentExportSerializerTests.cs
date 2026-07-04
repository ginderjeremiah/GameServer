using System.Text.Json;
using Game.Application.Content;
using Game.Core;
using Xunit;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// Unit coverage for the canonical export serialization (spike #1390, decision 2): the determinism rules
    /// that make committed diffs review-friendly and the CI drift guard byte-stable — id ordering, declared
    /// child-collection ordering, enum-name / UTC-ISO rendering, LF + trailing newline, and re-export
    /// idempotence.
    /// </summary>
    public class ContentExportSerializerTests
    {
        private static Contracts.Skill Skill(int id, IEnumerable<Contracts.SkillDamagePortion>? portions = null,
            IEnumerable<Contracts.AttributeMultiplier>? multipliers = null, IEnumerable<Contracts.SkillEffect>? effects = null,
            DateTime? retiredAt = null)
        {
            return new Contracts.Skill
            {
                Id = id,
                Name = $"Skill {id}",
                BaseDamage = 10m,
                Description = "desc",
                CooldownMs = 1000,
                IconPath = "",
                RarityId = ERarity.Common,
                Word = "",
                Pronunciation = "",
                Translation = "",
                Acquisition = ESkillAcquisition.Player,
                DesignerNotes = "",
                DamageMultipliers = multipliers ?? [],
                DamagePortions = portions ?? [new Contracts.SkillDamagePortion { Type = EDamageType.Physical, Weight = 1m }],
                Effects = effects ?? [],
                RetiredAt = retiredAt,
            };
        }

        [Fact]
        public void Canonicalize_OrdersTopLevelById()
        {
            var canonical = ContentExportSerializer.Canonicalize(new[] { Skill(2), Skill(0), Skill(1) });

            Assert.Equal([0, 1, 2], canonical.Select(s => s.Id));
        }

        [Fact]
        public void Canonicalize_OrdersChildCollectionsByDeclaredKey()
        {
            var skill = Skill(0,
                portions:
                [
                    new Contracts.SkillDamagePortion { Type = EDamageType.Fire, Weight = 1m },
                    new Contracts.SkillDamagePortion { Type = EDamageType.Physical, Weight = 2m },
                ],
                multipliers:
                [
                    new Contracts.AttributeMultiplier { AttributeId = EAttribute.Intellect, Multiplier = 1m },
                    new Contracts.AttributeMultiplier { AttributeId = EAttribute.Strength, Multiplier = 2m },
                ],
                effects:
                [
                    new Contracts.SkillEffect { Id = 5, Target = ESkillEffectTarget.Self, AttributeId = EAttribute.Strength, ModifierTypeId = EModifierType.Additive, Amount = 1m, DurationMs = 0, ScalingAttributeId = EAttribute.Strength, ScalingAmount = 0m },
                    new Contracts.SkillEffect { Id = 1, Target = ESkillEffectTarget.Self, AttributeId = EAttribute.Strength, ModifierTypeId = EModifierType.Additive, Amount = 1m, DurationMs = 0, ScalingAttributeId = EAttribute.Strength, ScalingAmount = 0m },
                ]);

            var canonical = ContentExportSerializer.Canonicalize([skill]).Single();

            Assert.Equal([EDamageType.Physical, EDamageType.Fire], canonical.DamagePortions.Select(p => p.Type));
            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], canonical.DamageMultipliers.Select(m => m.AttributeId));
            Assert.Equal([1, 5], canonical.Effects.Select(e => e.Id));
        }

        [Fact]
        public void Serialize_IsByteIdenticalAcrossInputOrderings()
        {
            var a = ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(new[] { Skill(0), Skill(1) }));
            var b = ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize(new[] { Skill(1), Skill(0) }));

            Assert.Equal(a, b);
        }

        [Fact]
        public void Serialize_UsesCamelCaseEnumNamesLfAndTrailingNewline()
        {
            var json = ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize([Skill(0)]));

            Assert.DoesNotContain("\r\n", json);
            Assert.EndsWith("\n", json);
            Assert.Contains("\"rarityId\": \"Common\"", json);
            Assert.Contains("\"acquisition\": \"Player\"", json);
            Assert.Contains("\"type\": \"Physical\"", json);
            // Two-space indentation.
            Assert.Contains("\n  {", json);
        }

        [Fact]
        public void Serialize_RendersRetiredAtAsUtcIsoOrNull()
        {
            var retired = ContentExportSerializer.Serialize(
                ContentExportSerializer.Canonicalize([Skill(0, retiredAt: new DateTime(2026, 6, 30, 12, 34, 56, DateTimeKind.Utc))]));
            var active = ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize([Skill(0)]));

            Assert.Contains("\"retiredAt\": \"2026-06-30T12:34:56.0000000Z\"", retired);
            Assert.Contains("\"retiredAt\": null", active);
        }

        [Fact]
        public void Serialize_RendersUnspecifiedKindAsUtcDeterministically()
        {
            // A bare `timestamp` column reads back as Unspecified; it must serialize identically to its UTC twin
            // (no locale-dependent ToUniversalTime conversion), so the drift guard is machine-independent.
            var unspecified = ContentExportSerializer.Serialize(
                ContentExportSerializer.Canonicalize([Skill(0, retiredAt: new DateTime(2026, 6, 30, 12, 34, 56, DateTimeKind.Unspecified))]));
            var utc = ContentExportSerializer.Serialize(
                ContentExportSerializer.Canonicalize([Skill(0, retiredAt: new DateTime(2026, 6, 30, 12, 34, 56, DateTimeKind.Utc))]));

            Assert.Equal(utc, unspecified);
        }

        [Fact]
        public void Serialize_ConvertsLocalKindToItsUtcInstant()
        {
            var local = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Local);
            var fromLocal = ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize([Skill(0, retiredAt: local)]));
            var fromUtc = ContentExportSerializer.Serialize(ContentExportSerializer.Canonicalize([Skill(0, retiredAt: local.ToUniversalTime())]));

            Assert.Equal(fromUtc, fromLocal);
        }

        [Fact]
        public void UtcIsoDateTimeConverter_RoundTripsToUtcInstant()
        {
            var options = new JsonSerializerOptions { Converters = { new ContentExportSerializer.UtcIsoDateTimeConverter() } };
            var original = new DateTime(2026, 6, 30, 12, 34, 56, DateTimeKind.Utc);

            var parsed = JsonSerializer.Deserialize<DateTime>(JsonSerializer.Serialize(original, options), options);

            Assert.Equal(original, parsed);
            Assert.Equal(DateTimeKind.Utc, parsed.Kind);
        }

        // --- Per-set child-collection ordering (the drift guard's seed slice leaves most of these empty) ------

        [Fact]
        public void Canonicalize_Items_OrdersChildrenByDeclaredKey()
        {
            var item = new Contracts.Item
            {
                Id = 0,
                Name = "Item",
                Description = "d",
                ItemCategoryId = EItemCategory.Weapon,
                RarityId = ERarity.Common,
                IconPath = "",
                DesignerNotes = "",
                Attributes =
                [
                    new Contracts.BattlerAttribute { AttributeId = EAttribute.Intellect, Amount = 1m },
                    new Contracts.BattlerAttribute { AttributeId = EAttribute.Strength, Amount = 2m },
                ],
                ModSlots =
                [
                    new Contracts.ItemModSlot { Id = 4, ItemId = 0, ItemModSlotTypeId = EItemModType.Prefix },
                    new Contracts.ItemModSlot { Id = 1, ItemId = 0, ItemModSlotTypeId = EItemModType.Suffix },
                ],
                Tags = [9, 2, 5],
            };

            var canonical = ContentExportSerializer.Canonicalize([item]).Single();

            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], canonical.Attributes.Select(a => a.AttributeId));
            Assert.Equal([1, 4], canonical.ModSlots.Select(s => s.Id));
            Assert.Equal([2, 5, 9], canonical.Tags);
        }

        [Fact]
        public void Canonicalize_ItemMods_OrdersChildrenByDeclaredKey()
        {
            var mod = new Contracts.ItemMod
            {
                Id = 0,
                Name = "Mod",
                Description = "d",
                ItemModTypeId = EItemModType.Prefix,
                RarityId = ERarity.Common,
                DesignerNotes = "",
                Attributes =
                [
                    new Contracts.BattlerAttribute { AttributeId = EAttribute.Luck, Amount = 1m },
                    new Contracts.BattlerAttribute { AttributeId = EAttribute.Strength, Amount = 2m },
                ],
                Tags = [3, 1],
            };

            var canonical = ContentExportSerializer.Canonicalize([mod]).Single();

            Assert.Equal([EAttribute.Strength, EAttribute.Luck], canonical.Attributes.Select(a => a.AttributeId));
            Assert.Equal([1, 3], canonical.Tags);
        }

        [Fact]
        public void Canonicalize_Enemies_OrdersChildrenIncludingSpawnTieBreak()
        {
            var enemy = new Contracts.Enemy
            {
                Id = 0,
                Name = "Enemy",
                IsBoss = false,
                DesignerNotes = "",
                AttributeDistribution =
                [
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Endurance, BaseAmount = 1m, AmountPerLevel = 0m },
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Strength, BaseAmount = 1m, AmountPerLevel = 0m },
                ],
                SkillPool = [3, 1],
                // Same ZoneId twice exercises the ThenBy(Weight) total-order tie-break.
                Spawns =
                [
                    new Contracts.EnemySpawn { ZoneId = 1, Weight = 5 },
                    new Contracts.EnemySpawn { ZoneId = 1, Weight = 2 },
                    new Contracts.EnemySpawn { ZoneId = 0, Weight = 9 },
                ],
            };

            var canonical = ContentExportSerializer.Canonicalize([enemy]).Single();

            Assert.Equal([EAttribute.Strength, EAttribute.Endurance], canonical.AttributeDistribution.Select(a => a.AttributeId));
            Assert.Equal([1, 3], canonical.SkillPool);
            Assert.Equal([(0, 9), (1, 2), (1, 5)], canonical.Spawns.Select(s => (s.ZoneId, s.Weight)));
        }

        [Fact]
        public void Canonicalize_Classes_OrdersChildrenIncludingEquipmentTieBreak()
        {
            var cls = new Contracts.Class
            {
                Id = 0,
                Name = "Class",
                Description = "d",
                Word = "w",
                PassiveAttributeId = EAttribute.Strength,
                PassiveAmount = 0m,
                PassiveScalingAttributeId = null,
                PassiveScalingAmount = 0m,
                PassiveModifierType = EModifierType.Additive,
                DesignerNotes = "",
                StarterSkillIds = [3, 1, 2],
                // Same slot twice exercises the ThenBy(ItemId) total-order tie-break.
                StarterEquipment =
                [
                    new Contracts.ClassStarterEquipment { ItemId = 7, EquipmentSlot = EEquipmentSlot.WeaponSlot },
                    new Contracts.ClassStarterEquipment { ItemId = 4, EquipmentSlot = EEquipmentSlot.WeaponSlot },
                    new Contracts.ClassStarterEquipment { ItemId = 1, EquipmentSlot = EEquipmentSlot.HelmSlot },
                ],
                AttributeDistributions =
                [
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Intellect, BaseAmount = 1m, AmountPerLevel = 0m },
                    new Contracts.AttributeDistribution { AttributeId = EAttribute.Strength, BaseAmount = 1m, AmountPerLevel = 0m },
                ],
            };

            var canonical = ContentExportSerializer.Canonicalize([cls]).Single();

            Assert.Equal([1, 2, 3], canonical.StarterSkillIds);
            Assert.Equal([EAttribute.Strength, EAttribute.Intellect], canonical.AttributeDistributions.Select(a => a.AttributeId));
            Assert.Equal(
                [(EEquipmentSlot.HelmSlot, 1), (EEquipmentSlot.WeaponSlot, 4), (EEquipmentSlot.WeaponSlot, 7)],
                canonical.StarterEquipment.Select(e => (e.EquipmentSlot, e.ItemId)));
        }

        [Fact]
        public void Canonicalize_Proficiencies_OrdersChildrenByDeclaredKey()
        {
            var proficiency = new Contracts.Proficiency
            {
                Id = 0,
                Name = "Prof",
                Description = "d",
                IconPath = "",
                Word = "w",
                Pronunciation = "p",
                Translation = "t",
                PathId = 0,
                PathOrdinal = 0,
                MaxLevel = 10,
                BaseXp = 1m,
                XpGrowth = 1m,
                DesignerNotes = "",
                LevelModifiers =
                [
                    new Contracts.ProficiencyLevelModifier { Level = 2, AttributeId = EAttribute.Strength, ModifierTypeId = EModifierType.Additive, Amount = 1m },
                    new Contracts.ProficiencyLevelModifier { Level = 1, AttributeId = EAttribute.Strength, ModifierTypeId = EModifierType.Additive, Amount = 1m },
                ],
                LevelRewards =
                [
                    new Contracts.ProficiencyLevelReward { Level = 5, RewardSkillId = 2 },
                    new Contracts.ProficiencyLevelReward { Level = 3, RewardSkillId = 1 },
                ],
                PrerequisiteIds = [4, 1],
            };

            var canonical = ContentExportSerializer.Canonicalize([proficiency]).Single();

            Assert.Equal([1, 2], canonical.LevelModifiers.Select(m => m.Level));
            Assert.Equal([3, 5], canonical.LevelRewards.Select(r => r.Level));
            Assert.Equal([1, 4], canonical.PrerequisiteIds);
        }

        [Fact]
        public void Canonicalize_SkillRecipes_OrdersChildrenByDeclaredKey()
        {
            var recipe = new Contracts.SkillRecipe
            {
                Id = 0,
                ResultSkillId = 9,
                DesignerNotes = "",
                InputSkillIds = [5, 2],
                Conditions =
                [
                    new Contracts.SkillRecipeCondition { ProficiencyId = 3, MinLevel = 1 },
                    new Contracts.SkillRecipeCondition { ProficiencyId = 1, MinLevel = 4 },
                ],
            };

            var canonical = ContentExportSerializer.Canonicalize([recipe]).Single();

            Assert.Equal([2, 5], canonical.InputSkillIds);
            Assert.Equal([1, 3], canonical.Conditions.Select(c => c.ProficiencyId));
        }

        [Fact]
        public void Canonicalize_Lessons_OrdersStepsByOrdinal()
        {
            var lesson = new Contracts.Lesson
            {
                Id = 0,
                Key = "idle-loop-basics",
                Name = "Idle Combat",
                TriggerType = ELessonTriggerType.ScreenVisit,
                ScreenKey = "fight",
                DesignerNotes = "",
                Steps =
                [
                    new Contracts.LessonStep { Ordinal = 2, Text = "Third" },
                    new Contracts.LessonStep { Ordinal = 0, Text = "First" },
                ],
            };

            var canonical = ContentExportSerializer.Canonicalize([lesson]).Single();

            Assert.Equal(["First", "Third"], canonical.Steps.Select(s => s.Text));
        }

        [Fact]
        public void Canonicalize_PathsAndZonesAndChallenges_OrderTopLevelById()
        {
            var paths = ContentExportSerializer.Canonicalize(new[]
            {
                new Contracts.Path { Id = 1, Name = "b", Description = "d", ActivityKey = EActivityKey.Physical, DesignerNotes = "" },
                new Contracts.Path { Id = 0, Name = "a", Description = "d", ActivityKey = EActivityKey.Physical, DesignerNotes = "" },
            });
            var zones = ContentExportSerializer.Canonicalize(new[]
            {
                new Contracts.Zone { Id = 1, Name = "b", Description = "d", DesignerNotes = "" },
                new Contracts.Zone { Id = 0, Name = "a", Description = "d", DesignerNotes = "" },
            });
            var challenges = ContentExportSerializer.Canonicalize(new[]
            {
                new Contracts.Challenge { Id = 1, Name = "b", Description = "d", DesignerNotes = "" },
                new Contracts.Challenge { Id = 0, Name = "a", Description = "d", DesignerNotes = "" },
            });

            Assert.Equal([0, 1], paths.Select(p => p.Id));
            Assert.Equal([0, 1], zones.Select(z => z.Id));
            Assert.Equal([0, 1], challenges.Select(c => c.Id));
        }

        [Fact]
        public void Canonicalize_Tags_OrderTopLevelById()
        {
            // Tags carry a non-zero-based identity (resolved by lookup, not index), so id order is still the
            // stable order the committed file is pinned to.
            var tags = ContentExportSerializer.Canonicalize(new[]
            {
                new Contracts.Tag { Id = 3, Name = "b", TagCategoryId = 1 },
                new Contracts.Tag { Id = 1, Name = "a", TagCategoryId = 1 },
            });

            Assert.Equal([1, 3], tags.Select(t => t.Id));
        }
    }
}
