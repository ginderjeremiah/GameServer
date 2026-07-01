using Game.Application.Content;
using Game.Core;
using Game.Core.Attributes;
using Xunit;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// Unit tests for the progression-graph lint (#1420). Each cross-entity check fires on a crafted broken
    /// graph and stays silent on a healthy one; a fully-consistent graph produces no findings at all.
    /// </summary>
    public class ProgressionGraphCheckerTests
    {
        private readonly ProgressionGraphChecker _checker = new();

        [Fact]
        public void HealthyGraph_ProducesNoFindings()
        {
            var findings = _checker.Check(HealthyGraph());
            Assert.Empty(findings);
        }

        // --- Dangling / retired references (Error) ----------------------------------------------------

        [Fact]
        public void Zone_BossReferencingMissingEnemy_IsError()
        {
            var graph = HealthyGraph() with { Zones = [Zone(0, bossEnemyId: 99)] };
            AssertHasFinding(graph, "ZoneBoss", ContentGraphSeverity.Error, "Zone", 0);
        }

        [Fact]
        public void Zone_BossReferencingRetiredEnemy_IsError()
        {
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, bossEnemyId: 1)],
                Enemies = [Enemy(1, isBoss: true, retiredAt: Retired)],
            };
            AssertHasFinding(graph, "ZoneBoss", ContentGraphSeverity.Error, "Zone", 0);
        }

        [Fact]
        public void Zone_BossReferencingNonBossEnemy_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, bossEnemyId: 1)],
                Enemies = [Enemy(1, isBoss: false, spawns: [(0, 1)])],
            };
            AssertHasFinding(graph, "ZoneBoss", ContentGraphSeverity.Warning, "Zone", 0);
        }

        [Fact]
        public void Zone_GatedByRetiredChallenge_IsError()
        {
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0)],
                Challenges = [Challenge(0, retiredAt: Retired)],
                Enemies = [Enemy(0, spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "ZoneUnlock", ContentGraphSeverity.Error, "Zone", 1);
        }

        [Fact]
        public void Challenge_TargetingMissingEnemy_IsError()
        {
            var graph = HealthyGraph() with
            {
                Challenges = [Challenge(0, entityType: EEntityType.Enemy, targetEntityId: 99)],
            };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Error, "Challenge", 0);
        }

        [Fact]
        public void Challenge_TargetingRetiredZone_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Challenges = [Challenge(0, entityType: EEntityType.Zone, targetEntityId: 5)],
                Zones = HealthyGraph().Zones.Append(Zone(5, retiredAt: Retired)).ToList(),
            };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Warning, "Challenge", 0);
        }

        [Fact]
        public void Challenge_RewardingMissingItem_IsError()
        {
            var graph = HealthyGraph() with { Challenges = [Challenge(0, rewardItemId: 99)] };
            AssertHasFinding(graph, "ChallengeReward", ContentGraphSeverity.Error, "Challenge", 0);
        }

        // --- Enemies ----------------------------------------------------------------------------------

        [Fact]
        public void Enemy_SkillPoolWithMissingSkill_IsError()
        {
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [99], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemySkill", ContentGraphSeverity.Error, "Enemy", 0);
        }

        [Fact]
        public void Enemy_SkillPoolWithNonEnemyFlaggedSkill_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                // Skill 1 is Player-flagged, not Enemy-flagged.
                Enemies = [Enemy(0, skillPool: [1], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemySkill", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_SpawningInHomeZone_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1), (2, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemySpawn", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        // --- Classes ----------------------------------------------------------------------------------

        [Fact]
        public void Class_StarterSkillMissing_IsError()
        {
            var graph = HealthyGraph() with { Classes = [Class(0, starterSkills: [99])] };
            AssertHasFinding(graph, "ClassStarterSkill", ContentGraphSeverity.Error, "Class", 0);
        }

        [Fact]
        public void Class_StarterSkillNotPlayerFlagged_IsWarning()
        {
            var graph = HealthyGraph() with { Classes = [Class(0, starterSkills: [3])] }; // skill 3 is Item-only
            AssertHasFinding(graph, "ClassStarterSkill", ContentGraphSeverity.Warning, "Class", 0);
        }

        // --- Items ------------------------------------------------------------------------------------

        [Fact]
        public void Item_WeaponWithoutWeaponType_IsError()
        {
            var graph = HealthyGraph() with
            {
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: 3, weaponType: null)],
            };
            AssertHasFinding(graph, "WeaponStranding", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_WeaponWithoutGrantedSkill_IsError()
        {
            var graph = HealthyGraph() with
            {
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: null, weaponType: EDamageType.Sword)],
            };
            AssertHasFinding(graph, "WeaponStranding", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_NonWeaponWithWeaponType_IsError()
        {
            var graph = HealthyGraph() with
            {
                Items = [Item(0, category: EItemCategory.Accessory, weaponType: EDamageType.Sword)],
            };
            AssertHasFinding(graph, "WeaponStranding", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_GrantingNonItemFlaggedSkill_IsWarning()
        {
            var graph = HealthyGraph() with { Items = [Item(0, grantedSkillId: 1)] }; // skill 1 is Player-only
            AssertHasFinding(graph, "ItemGrant", ContentGraphSeverity.Warning, "Item", 0);
        }

        [Fact]
        public void Item_GatedByMissingProficiency_IsError()
        {
            var graph = HealthyGraph() with { Items = [Item(0, requiredProficiencyId: 99, requiredProficiencyLevel: 1)] };
            AssertHasFinding(graph, "ItemProficiencyGate", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_GatedByRetiredProficiency_IsError()
        {
            var graph = HealthyGraph() with
            {
                Items = [Item(0, requiredProficiencyId: 0, requiredProficiencyLevel: 1)],
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, retiredAt: Retired)],
            };
            AssertHasFinding(graph, "ItemProficiencyGate", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_GatedAboveProficiencyMaxLevel_IsError()
        {
            var graph = HealthyGraph() with { Items = [Item(0, requiredProficiencyId: 0, requiredProficiencyLevel: 999)] };
            AssertHasFinding(graph, "ItemProficiencyGate", ContentGraphSeverity.Error, "Item", 0);
        }

        // --- Proficiencies ----------------------------------------------------------------------------

        [Fact]
        public void Proficiency_RewardingMissingSkill_IsError()
        {
            var graph = HealthyGraph() with
            {
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(5, 99)])],
            };
            AssertHasFinding(graph, "ProficiencyReward", ContentGraphSeverity.Error, "Proficiency", 0);
        }

        [Fact]
        public void Proficiency_RewardAboveMaxLevel_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(50, 5)])],
            };
            AssertHasFinding(graph, "ProficiencyReward", ContentGraphSeverity.Warning, "Proficiency", 0);
        }

        [Fact]
        public void Proficiency_PrerequisiteOnRetiredPath_IsError()
        {
            var graph = HealthyGraph() with
            {
                Paths = [Path(0), Path(1, retiredAt: Retired)],
                Proficiencies =
                [
                    Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(5, 5)], prerequisiteIds: [1]),
                    Proficiency(1, pathId: 1, maxLevel: 10),
                ],
            };
            AssertHasFinding(graph, "ProficiencyPrerequisite", ContentGraphSeverity.Error, "Proficiency", 0);
        }

        [Fact]
        public void Proficiency_OnMissingPath_IsError()
        {
            var graph = HealthyGraph() with
            {
                Proficiencies = [Proficiency(0, pathId: 99, maxLevel: 10, rewards: [(5, 5)])],
            };
            AssertHasFinding(graph, "ProficiencyPath", ContentGraphSeverity.Error, "Proficiency", 0);
        }

        // --- Skill recipes ----------------------------------------------------------------------------

        [Fact]
        public void Recipe_ResultNotSynthesisFlagged_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                // Skill 5 is Player-only, not Synthesis-flagged.
                SkillRecipes = [Recipe(0, resultSkillId: 5, inputs: [1])],
            };
            AssertHasFinding(graph, "RecipeResult", ContentGraphSeverity.Warning, "SkillRecipe", 0);
        }

        [Fact]
        public void Recipe_InputWithNoAcquisitionPath_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills
                    .Append(Skill(6, ESkillAcquisition.Synthesis))   // result
                    .Append(Skill(7, ESkillAcquisition.Player))      // input, but nothing grants it
                    .ToList(),
                SkillRecipes = [Recipe(0, resultSkillId: 6, inputs: [7])],
            };
            AssertHasFinding(graph, "RecipeInputOwnable", ContentGraphSeverity.Warning, "SkillRecipe", 0);
        }

        [Fact]
        public void Recipe_InputOwnableViaClassKit_IsSilent()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Synthesis)).ToList(),
                // Skill 1 is a class starter, so it is ownable and the recipe input is satisfied.
                SkillRecipes = [Recipe(0, resultSkillId: 6, inputs: [1])],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "RecipeInputOwnable");
        }

        // --- Orphan skills ----------------------------------------------------------------------------

        [Fact]
        public void OrphanSkill_ItemFlaggedWithNoGrantingItem_IsWarning()
        {
            var graph = HealthyGraph() with { Items = [] }; // skill 3 is Item-flagged; drop the granting item
            AssertHasFinding(graph, "OrphanSkill", ContentGraphSeverity.Warning, "Skill", 3);
        }

        [Fact]
        public void OrphanSkill_PunchIsExempt()
        {
            // Punch (id 0) is Player-flagged and granted by no kit/milestone, but is the weapon-system signature.
            var findings = _checker.Check(HealthyGraph());
            Assert.DoesNotContain(findings, f => f.Check == "OrphanSkill" && f.EntityId == GameConstants.PunchSkillId);
        }

        // --- Zone reachability & structure ------------------------------------------------------------

        [Fact]
        public void Zone_UnreachableViaCyclicUnlock_IsWarning()
        {
            // Zones 3 and 4 gate each other: neither roots at the open start zone.
            var graph = HealthyGraph() with
            {
                Zones =
                [
                    Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true),
                    Zone(3, unlockChallengeId: 1), Zone(4, unlockChallengeId: 2),
                ],
                Challenges =
                [
                    Challenge(0, entityType: EEntityType.Zone, targetEntityId: 0),
                    Challenge(1, entityType: EEntityType.Zone, targetEntityId: 4),
                    Challenge(2, entityType: EEntityType.Zone, targetEntityId: 3),
                ],
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1), (3, 1), (4, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "ZoneReachability", ContentGraphSeverity.Warning, "Zone", 3);
            AssertHasFinding(graph, "ZoneReachability", ContentGraphSeverity.Warning, "Zone", 4);
        }

        [Fact]
        public void Zone_NoOpenStartZone_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges = [Challenge(0, entityType: EEntityType.Zone, targetEntityId: 0)],
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "ZoneReachability", ContentGraphSeverity.Warning, "Zone", 0);
        }

        [Fact]
        public void Zone_MoreThanOneLiveHome_IsError()
        {
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true), Zone(3, isHome: true)],
            };
            AssertHasFinding(graph, "SingleHomeZone", ContentGraphSeverity.Error, "Zone", 2);
            AssertHasFinding(graph, "SingleHomeZone", ContentGraphSeverity.Error, "Zone", 3);
        }

        [Fact]
        public void Zone_LiveCombatZoneWithNoEnemies_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                // Enemy 0 no longer spawns in zone 1.
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EmptyCombatZone", ContentGraphSeverity.Warning, "Zone", 1);
        }

        [Fact]
        public void RetiredSourceRecords_AreNotCheckedAsSources()
        {
            // A retired zone with a dangling boss reference is out of circulation, so it raises nothing.
            var graph = HealthyGraph() with
            {
                Zones = HealthyGraph().Zones.Append(Zone(9, bossEnemyId: 99, retiredAt: Retired)).ToList(),
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.EntityId == 9);
        }

        // === Fixtures =================================================================================

        private static readonly DateTime Retired = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private void AssertHasFinding(ContentGraph graph, string check, ContentGraphSeverity severity, string kind, int entityId)
        {
            var findings = _checker.Check(graph);
            Assert.Contains(findings, f => f.Check == check && f.Severity == severity && f.EntityKind == kind && f.EntityId == entityId);
        }

        /// <summary>A small, fully-consistent graph: open start zone 0 + gated zone 1 (both populated), a Home
        /// zone, a boss, an enemy pool skill, an item-granted skill, and a proficiency milestone — no findings.</summary>
        private static ContentGraph HealthyGraph()
        {
            return new ContentGraph(
                Skills:
                [
                    Skill(0, ESkillAcquisition.Player),  // Punch (exempt)
                    Skill(1, ESkillAcquisition.Player),  // class starter
                    Skill(2, ESkillAcquisition.Enemy),   // enemy pool
                    Skill(3, ESkillAcquisition.Item),    // item grant
                    Skill(5, ESkillAcquisition.Player),  // milestone
                ],
                Items: [Item(0, grantedSkillId: 3)],
                ItemMods: [],
                Enemies: [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
                Zones: [Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges: [Challenge(0, entityType: EEntityType.Zone, targetEntityId: 0)],
                Classes: [Class(0, starterSkills: [1])],
                Paths: [Path(0)],
                Proficiencies: [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(5, 5)])],
                SkillRecipes: []);
        }

        private static Contracts.Skill Skill(int id, ESkillAcquisition acquisition, DateTime? retiredAt = null) => new()
        {
            Id = id,
            Name = $"Skill {id}",
            BaseDamage = 1,
            DamageMultipliers = [],
            Effects = [],
            DamagePortions = [new Contracts.SkillDamagePortion { Type = EDamageType.Physical, Weight = 1 }],
            Description = "",
            CooldownMs = 1000,
            IconPath = "",
            RarityId = ERarity.Common,
            Word = "",
            Pronunciation = "",
            Translation = "",
            Acquisition = acquisition,
            RetiredAt = retiredAt,
        };

        private static Contracts.Item Item(
            int id,
            EItemCategory category = EItemCategory.Accessory,
            int? grantedSkillId = null,
            EDamageType? weaponType = null,
            int? requiredProficiencyId = null,
            int requiredProficiencyLevel = 0,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = $"Item {id}",
                Description = "",
                ItemCategoryId = category,
                RarityId = ERarity.Common,
                IconPath = "",
                Attributes = [],
                ModSlots = [],
                Tags = [],
                GrantedSkillId = grantedSkillId,
                WeaponType = weaponType,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                RetiredAt = retiredAt,
            };

        private static Contracts.Enemy Enemy(
            int id,
            bool isBoss = false,
            int[]? skillPool = null,
            (int zoneId, int weight)[]? spawns = null,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = $"Enemy {id}",
                IsBoss = isBoss,
                AttributeDistribution = [],
                SkillPool = skillPool ?? [],
                Spawns = (spawns ?? []).Select(s => new Contracts.EnemySpawn { ZoneId = s.zoneId, Weight = s.weight }).ToList(),
                RetiredAt = retiredAt,
            };

        private static Contracts.Zone Zone(
            int id,
            bool isHome = false,
            int? bossEnemyId = null,
            int? unlockChallengeId = null,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = $"Zone {id}",
                Description = "",
                Order = id,
                LevelMin = 1,
                LevelMax = 10,
                BossEnemyId = bossEnemyId,
                BossLevel = 1,
                UnlockChallengeId = unlockChallengeId,
                IsHome = isHome,
                RetiredAt = retiredAt,
            };

        private static Contracts.Challenge Challenge(
            int id,
            EEntityType entityType = EEntityType.None,
            int? targetEntityId = null,
            int? rewardItemId = null,
            int? rewardItemModId = null,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = $"Challenge {id}",
                Description = "",
                ChallengeTypeId = EChallengeType.ZonesCleared,
                StatisticType = EStatisticType.ZonesCleared,
                EntityType = entityType,
                TargetEntityId = targetEntityId,
                ProgressGoal = 1,
                RewardItemId = rewardItemId,
                RewardItemModId = rewardItemModId,
                RetiredAt = retiredAt,
            };

        private static Contracts.Class Class(int id, int[]? starterSkills = null, int[]? starterEquipmentItemIds = null, DateTime? retiredAt = null) => new()
        {
            Id = id,
            Name = $"Class {id}",
            Description = "",
            Word = "",
            PassiveAttributeId = EAttribute.Strength,
            PassiveAmount = 0,
            PassiveScalingAttributeId = null,
            PassiveScalingAmount = 0,
            PassiveModifierType = EModifierType.Additive,
            StarterSkillIds = starterSkills ?? [],
            StarterEquipment = (starterEquipmentItemIds ?? []).Select((itemId, i) => new Contracts.ClassStarterEquipment { ItemId = itemId, EquipmentSlot = (EEquipmentSlot)i }).ToList(),
            AttributeDistributions = [],
            RetiredAt = retiredAt,
        };

        private static Contracts.Path Path(int id, DateTime? retiredAt = null) => new()
        {
            Id = id,
            Name = $"Path {id}",
            Description = "",
            ActivityKey = default,
            RetiredAt = retiredAt,
        };

        private static Contracts.Proficiency Proficiency(
            int id,
            int pathId = 0,
            int maxLevel = 10,
            (int level, int rewardSkillId)[]? rewards = null,
            int[]? prerequisiteIds = null,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = $"Proficiency {id}",
                Description = "",
                IconPath = "",
                Word = "",
                Pronunciation = "",
                Translation = "",
                PathId = pathId,
                PathOrdinal = 0,
                MaxLevel = maxLevel,
                BaseXp = 1,
                XpGrowth = 1,
                RetiredAt = retiredAt,
                LevelModifiers = [],
                LevelRewards = (rewards ?? []).Select(r => new Contracts.ProficiencyLevelReward { Level = r.level, RewardSkillId = r.rewardSkillId }).ToList(),
                PrerequisiteIds = prerequisiteIds ?? [],
            };

        private static Contracts.SkillRecipe Recipe(int id, int resultSkillId, int[]? inputs = null, (int proficiencyId, int minLevel)[]? conditions = null, DateTime? retiredAt = null) => new()
        {
            Id = id,
            ResultSkillId = resultSkillId,
            RetiredAt = retiredAt,
            InputSkillIds = inputs ?? [],
            Conditions = (conditions ?? []).Select(c => new Contracts.SkillRecipeCondition { ProficiencyId = c.proficiencyId, MinLevel = c.minLevel }).ToList(),
        };
    }
}
