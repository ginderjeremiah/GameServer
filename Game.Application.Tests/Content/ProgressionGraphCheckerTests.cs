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

        [Fact]
        public void Challenge_KillsByDamageTypeWithNoTarget_IsError()
        {
            // KillsByDamageType writes only per-damage-type-key statistic rows, no global row — a challenge
            // with no target can never track progress (#1455).
            var graph = HealthyGraph() with
            {
                Challenges = [Challenge(0, challengeTypeId: EChallengeType.KillsByDamageType, targetEntityId: null)],
            };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Error, "Challenge", 0);
        }

        [Fact]
        public void Challenge_ZonesClearedTargetingBossLessZone_IsError()
        {
            // Zone 0's dedicated boss is what ZonesCleared records against (game-design.md § Zone Clears); a
            // boss-less target can never clear, the exact analogue of the KillsByDamageType null-target case.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges = [Challenge(0, challengeTypeId: EChallengeType.ZonesCleared, entityType: EEntityType.Zone, targetEntityId: 0)],
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Error, "Challenge", 0);
        }

        [Fact]
        public void Challenge_ZonesClearedTargetingBossedZone_ProducesNoChallengeTargetFinding()
        {
            Assert.DoesNotContain(_checker.Check(HealthyGraph()), f => f.Check == "ChallengeTarget");
        }

        [Fact]
        public void Challenge_ZonesClearedTargetingBossLessZone_AlsoMakesGatedZoneUnreachable()
        {
            // Zone 1's unlock is gated on clearing boss-less zone 0, which can never happen — the gate never
            // opens, so zone 1 is unreachable too (mirrors the ChallengeTarget finding on the challenge itself).
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges = [Challenge(0, challengeTypeId: EChallengeType.ZonesCleared, entityType: EEntityType.Zone, targetEntityId: 0)],
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "ZoneReachability", ContentGraphSeverity.Warning, "Zone", 1);
        }

        [Fact]
        public void Challenge_ProgressGoalZero_IsWarning()
        {
            var graph = HealthyGraph() with { Challenges = [Challenge(0, progressGoal: 0)] };
            AssertHasFinding(graph, "ChallengeProgressGoal", ContentGraphSeverity.Warning, "Challenge", 0);
        }

        [Fact]
        public void Challenge_ProgressGoalNegative_IsWarning()
        {
            var graph = HealthyGraph() with { Challenges = [Challenge(0, progressGoal: -5)] };
            AssertHasFinding(graph, "ChallengeProgressGoal", ContentGraphSeverity.Warning, "Challenge", 0);
        }

        [Fact]
        public void Challenge_KillsByDamageTypeWithValidTarget_ProducesNoFinding()
        {
            var graph = HealthyGraph() with
            {
                Challenges =
                [
                    Challenge(0, challengeTypeId: EChallengeType.KillsByDamageType,
                        entityType: EEntityType.DamageType, targetEntityId: (int)EDamageTypeKey.Fire),
                ],
            };
            Assert.Empty(_checker.Check(graph));
        }

        [Fact]
        public void Challenge_TargetingUndefinedDamageTypeKey_IsError()
        {
            var graph = HealthyGraph() with
            {
                Challenges =
                [
                    Challenge(0, challengeTypeId: EChallengeType.KillsByDamageType,
                        entityType: EEntityType.DamageType, targetEntityId: 999),
                ],
            };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Error, "Challenge", 0);
        }

        [Fact]
        public void Challenge_BossOnlyTypeTargetingNonBossEnemy_IsWarning()
        {
            // BossesDefeated records per-boss rows only; targeting non-boss enemy 0 can never progress.
            var graph = HealthyGraph() with
            {
                Challenges =
                [
                    Challenge(0, challengeTypeId: EChallengeType.BossesDefeated,
                        entityType: EEntityType.Enemy, targetEntityId: 0),
                ],
            };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Warning, "Challenge", 0);
        }

        [Fact]
        public void Challenge_BossOnlyTypeTargetingBossEnemy_ProducesNoFinding()
        {
            // Enemy 1 is a boss; making it zone 0's boss keeps the gate on challenge 0 reachable.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, bossEnemyId: 1), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges =
                [
                    Challenge(0, challengeTypeId: EChallengeType.BossesDefeated,
                        entityType: EEntityType.Enemy, targetEntityId: 1),
                ],
            };
            Assert.Empty(_checker.Check(graph));
        }

        [Fact]
        public void Challenge_NonBossOnlyTypeTargetingNonBossEnemy_ProducesNoFinding()
        {
            var graph = HealthyGraph() with
            {
                Challenges =
                [
                    Challenge(0, challengeTypeId: EChallengeType.EnemiesKilled,
                        entityType: EEntityType.Enemy, targetEntityId: 0),
                ],
            };
            Assert.Empty(_checker.Check(graph));
        }

        [Fact]
        public void Challenge_BossOnlyTypeTargetingRetiredNonBossEnemy_WarnsOnlyAboutRetirement()
        {
            // A retired target already gets its own warning; the boss-ness warning is skipped for it,
            // mirroring the ZoneBoss precedent.
            var graph = HealthyGraph() with
            {
                Challenges =
                [
                    Challenge(0, challengeTypeId: EChallengeType.BossesDefeated,
                        entityType: EEntityType.Enemy, targetEntityId: 5),
                ],
                Enemies = HealthyGraph().Enemies.Append(Enemy(5, retiredAt: Retired)).ToList(),
            };
            var finding = Assert.Single(_checker.Check(graph), f => f.Check == "ChallengeTarget");
            Assert.Contains("retired", finding.Message);
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

        [Fact]
        public void Enemy_SpawnWithNegativeWeight_IsError()
        {
            // ProbabilityTable rejects a negative weight outright, crashing the whole zone's spawn-table build.
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, -1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemySpawnWeight", ContentGraphSeverity.Error, "Enemy", 0);
        }

        [Fact]
        public void Enemy_SpawnWithZeroWeight_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 0), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemySpawnWeight", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        // --- Enemy attribute consumption (dead-stat XP inflation, #1529) ------------------------------

        [Fact]
        public void Enemy_DistributesStrengthAndEndurance_ProducesNoFinding()
        {
            // Both feed the always-live MaxHealth/Toughness static derivation, regardless of the kit.
            var graph = HealthyGraph() with
            {
                Enemies =
                [
                    Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)],
                        attributeDistribution: [(EAttribute.Strength, 5, 1), (EAttribute.Endurance, 5, 1)]),
                    Enemy(1, isBoss: true),
                ],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_DistributesAgilityWithNoKitConsumer_IsWarning()
        {
            // Agility's only static derivations feed the crit/dodge/parry-multiplier family, which the engine
            // never rolls for an enemy — dead weight absent a direct kit consumer.
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Agility, 5, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_DistributesLuckWithNoKitConsumer_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Luck, 5, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_DistributesIntellectWithNoKitConsumer_IsWarning()
        {
            // Intellect (like Dexterity) has no static derivation at all, so it needs a direct kit consumer.
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Intellect, 5, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_DistributesAgilityConsumedByPooledDamageMultiplier_ProducesNoFinding()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Enemy, damageMultipliers: [(EAttribute.Agility, 1.5m)])).ToList(),
                Enemies = [Enemy(0, skillPool: [6], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Agility, 5, 1)]), Enemy(1, isBoss: true)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_DistributesAgilityWithCoAuthoredCooldownBonus_ProducesNoFinding()
        {
            // Agility has no direct kit consumer here, but co-authoring CooldownBonus makes its derived
            // CooldownBonusMultiplier live (CooldownBonus × CooldownBonusMultiplier feeds the cooldown rate),
            // so Agility is no longer dead weight — the exact case #1581 moved off the heuristic for.
            var graph = HealthyGraph() with
            {
                Enemies =
                [
                    Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)],
                        attributeDistribution: [(EAttribute.Agility, 5, 1), (EAttribute.CooldownBonus, 5, 1)]),
                    Enemy(1, isBoss: true),
                ],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_DistributesLuckConsumedByPooledEffectScaling_ProducesNoFinding()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Enemy, effectScaling: [(EAttribute.Luck, 0.5m)])).ToList(),
                Enemies = [Enemy(0, skillPool: [6], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Luck, 5, 1)]), Enemy(1, isBoss: true)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_DistributesAttributeConsumedOnlyByZeroScalingEffect_IsWarning()
        {
            // ScalingAmount 0 means the effect doesn't actually scale off the attribute (the field is just unset).
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Enemy, effectScaling: [(EAttribute.Intellect, 0m)])).ToList(),
                Enemies = [Enemy(0, skillPool: [6], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Intellect, 5, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_DistributesAttributeConsumedOnlyByZeroDamageMultiplier_IsWarning()
        {
            // Multiplier 0 is equally inert, mirroring the zero-ScalingAmount effect case above.
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Enemy, damageMultipliers: [(EAttribute.Intellect, 0m)])).ToList(),
                Enemies = [Enemy(0, skillPool: [6], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Intellect, 5, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_DistributesAttributeConsumedOnlyByRetiredPooledSkill_IsWarning()
        {
            // Skill 6 is retired, so it's out of circulation and can't excuse the distribution point.
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Enemy, retiredAt: Retired, damageMultipliers: [(EAttribute.Intellect, 1)])).ToList(),
                Enemies = [Enemy(0, skillPool: [6], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Intellect, 5, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 0);
        }

        [Fact]
        public void Enemy_DistributesZeroAmountAttribute_ProducesNoFinding()
        {
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)], attributeDistribution: [(EAttribute.Agility, 0, 0)]), Enemy(1, isBoss: true)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_RetiredWithInertAttribute_ProducesNoFinding()
        {
            // A retired enemy is out of circulation, so its dead stat isn't worth flagging.
            var graph = HealthyGraph() with
            {
                Enemies = HealthyGraph().Enemies
                    .Append(Enemy(9, skillPool: [2], spawns: [(0, 1)], retiredAt: Retired, attributeDistribution: [(EAttribute.Luck, 5, 1)]))
                    .ToList(),
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_NotPlacedInAnyLiveZone_ProducesNoFinding()
        {
            // No spawn table entry and no dedicated-boss slot means no representative level to rate at, so the
            // check is skipped rather than rating at a made-up level.
            var graph = HealthyGraph() with
            {
                Enemies = HealthyGraph().Enemies
                    .Append(Enemy(9, skillPool: [2], spawns: [], attributeDistribution: [(EAttribute.Luck, 5, 1)]))
                    .ToList(),
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "EnemyInertAttribute");
        }

        [Fact]
        public void Enemy_PlacedOnlyAsZoneBoss_IsRatedAtZoneBossLevel()
        {
            // Enemy 9 carries no spawn-table entry at all — its only live placement is zone 1's dedicated boss
            // slot — so a representative level can only come from ResolveRatingLevel's boss-level branch
            // (zone.BossLevel). Its kit (skill 2, same as the sibling AGI-dead-stat tests above) doesn't consume
            // Agility, so a live rating (i.e. the branch actually firing rather than being skipped as unplaced)
            // must still surface the dead-stat warning.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0, bossEnemyId: 9, bossLevel: 5), Zone(2, isHome: true)],
                Enemies = HealthyGraph().Enemies
                    .Append(Enemy(9, isBoss: true, skillPool: [2], spawns: [], attributeDistribution: [(EAttribute.Agility, 5, 1)]))
                    .ToList(),
            };
            AssertHasFinding(graph, "EnemyInertAttribute", ContentGraphSeverity.Warning, "Enemy", 9);
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

        [Fact]
        public void Item_ReferencingMissingTag_IsError()
        {
            var graph = HealthyGraph() with { Items = [Item(0, grantedSkillId: 3, tags: [99])] };
            AssertHasFinding(graph, "ItemTag", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_ReferencingExistingTag_ProducesNoFinding()
        {
            var graph = HealthyGraph() with { Tags = [Tag(0)], Items = [Item(0, grantedSkillId: 3, tags: [0])] };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "ItemTag");
        }

        // --- Item mods ----------------------------------------------------------------------------------

        [Fact]
        public void ItemMod_ReferencingMissingTag_IsError()
        {
            var graph = HealthyGraph() with { ItemMods = [ItemMod(0, tags: [99])] };
            AssertHasFinding(graph, "ItemModTag", ContentGraphSeverity.Error, "ItemMod", 0);
        }

        [Fact]
        public void ItemMod_ReferencingExistingTag_ProducesNoFinding()
        {
            var graph = HealthyGraph() with { Tags = [Tag(0)], ItemMods = [ItemMod(0, tags: [0])] };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "ItemModTag");
        }

        [Fact]
        public void ItemMod_Retired_IsNotCheckedAsSource()
        {
            // A retired item mod is out of circulation, so a dangling tag it carries is harmless.
            var graph = HealthyGraph() with { ItemMods = [ItemMod(0, tags: [99], retiredAt: Retired)] };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "ItemModTag");
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
        public void Zone_DuplicateOrder_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, bossEnemyId: 1, order: 0), Zone(1, unlockChallengeId: 0, order: 0), Zone(2, isHome: true, order: 2)],
            };
            AssertHasFinding(graph, "ZoneOrder", ContentGraphSeverity.Warning, "Zone", 0);
            AssertHasFinding(graph, "ZoneOrder", ContentGraphSeverity.Warning, "Zone", 1);
        }

        [Fact]
        public void Zone_LevelMinGreaterThanLevelMax_IsError()
        {
            // The domain Zone model throws at construction on this, so it's a build-crashing Error, not a Warning.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, bossEnemyId: 1, levelMin: 10, levelMax: 5), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
            };
            AssertHasFinding(graph, "ZoneLevelRange", ContentGraphSeverity.Error, "Zone", 0);
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

        [Fact]
        public void Challenge_TargetingMissingSkill_IsError()
        {
            var graph = HealthyGraph() with { Challenges = [Challenge(0, entityType: EEntityType.Skill, targetEntityId: 99)] };
            AssertHasFinding(graph, "ChallengeTarget", ContentGraphSeverity.Error, "Challenge", 0);
        }

        [Fact]
        public void Challenge_WithNoneEntityTargetId_ChecksNothing()
        {
            // A None-typed challenge with a stray target id resolves against no set; it must raise nothing.
            var graph = HealthyGraph() with { Challenges = [Challenge(0, entityType: EEntityType.None, targetEntityId: 99)] };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "ChallengeTarget");
        }

        [Fact]
        public void Challenge_RewardingMissingItemMod_IsError()
        {
            var graph = HealthyGraph() with { Challenges = [Challenge(0, rewardItemModId: 99)] };
            AssertHasFinding(graph, "ChallengeReward", ContentGraphSeverity.Error, "Challenge", 0);
        }

        [Fact]
        public void Enemy_SpawningInMissingZone_IsError()
        {
            var graph = HealthyGraph() with
            {
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1), (99, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "EnemySpawn", ContentGraphSeverity.Error, "Enemy", 0);
        }

        [Fact]
        public void Class_StarterEquipmentReferencingMissingItem_IsError()
        {
            var graph = HealthyGraph() with { Classes = [Class(0, starterSkills: [1], starterEquipmentItemIds: [99])] };
            AssertHasFinding(graph, "ClassStarterItem", ContentGraphSeverity.Error, "Class", 0);
        }

        [Fact]
        public void Item_WeaponWithUndefinedWeaponType_IsError()
        {
            var graph = HealthyGraph() with
            {
                // Any defined leaf (including a non-martial caster type like Fire, #1456) is valid; an
                // out-of-range value is the only rejection left.
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: 3, weaponType: (EDamageType)999)],
            };
            AssertHasFinding(graph, "WeaponStranding", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_WeaponSignatureOfDifferentWeaponLeaf_IsError()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Item, primaryType: EDamageType.Axe)).ToList(),
                // An Axe-typed signature never matches a Sword weapon — the gate dims it, stranding the weapon.
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: 6, weaponType: EDamageType.Sword)],
            };
            AssertHasFinding(graph, "WeaponStranding", ContentGraphSeverity.Error, "Item", 0);
        }

        [Fact]
        public void Item_WeaponSignatureMatchingWeaponType_IsSilent()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Item, primaryType: EDamageType.Sword)).ToList(),
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: 6, weaponType: EDamageType.Sword)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.EntityKind == "Item" && f.EntityId == 0);
        }

        [Fact]
        public void Item_CasterWeaponWithMatchingElementalSignature_IsSilent()
        {
            var graph = HealthyGraph() with
            {
                // A Fire staff (WeaponType = Fire) granting a Fire-typed signature: a caster weapon declaring
                // its element rather than a martial leaf (#1456).
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Item, primaryType: EDamageType.Fire)).ToList(),
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: 6, weaponType: EDamageType.Fire)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.EntityKind == "Item" && f.EntityId == 0);
        }

        [Fact]
        public void Item_WeaponSignatureOfNonMartialType_IsSilentRegardlessOfWeaponType()
        {
            var graph = HealthyGraph() with
            {
                // A Sword weapon granting a Fire-typed signature: non-martial signatures are never gated, so
                // they always qualify even though they don't match the weapon's own (martial) type.
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Item, primaryType: EDamageType.Fire)).ToList(),
                Items = [Item(0, category: EItemCategory.Weapon, grantedSkillId: 6, weaponType: EDamageType.Sword)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.EntityKind == "Item" && f.EntityId == 0);
        }

        [Fact]
        public void Proficiency_RewardingNonPlayerFlaggedSkill_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                // Skill 3 is Item-only, not Player-flagged.
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(5, 3)])],
            };
            AssertHasFinding(graph, "ProficiencyReward", ContentGraphSeverity.Warning, "Proficiency", 0);
        }

        [Fact]
        public void Proficiency_RewardBelowLevelOne_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(0, 5)])],
            };
            AssertHasFinding(graph, "ProficiencyReward", ContentGraphSeverity.Warning, "Proficiency", 0);
        }

        [Fact]
        public void Proficiency_PrerequisiteMissing_IsError()
        {
            var graph = HealthyGraph() with
            {
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(5, 5)], prerequisiteIds: [99])],
            };
            AssertHasFinding(graph, "ProficiencyPrerequisite", ContentGraphSeverity.Error, "Proficiency", 0);
        }

        [Fact]
        public void Recipe_ResultSkillMissing_IsError()
        {
            var graph = HealthyGraph() with { SkillRecipes = [Recipe(0, resultSkillId: 99, inputs: [1])] };
            AssertHasFinding(graph, "RecipeResult", ContentGraphSeverity.Error, "SkillRecipe", 0);
        }

        [Fact]
        public void Recipe_InputSkillMissing_IsError()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Synthesis)).ToList(),
                SkillRecipes = [Recipe(0, resultSkillId: 6, inputs: [1, 99])],
            };
            AssertHasFinding(graph, "RecipeInput", ContentGraphSeverity.Error, "SkillRecipe", 0);
        }

        [Fact]
        public void Recipe_ConditionOnMissingProficiency_IsError()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Synthesis)).ToList(),
                SkillRecipes = [Recipe(0, resultSkillId: 6, inputs: [1], conditions: [(99, 1)])],
            };
            AssertHasFinding(graph, "RecipeCondition", ContentGraphSeverity.Error, "SkillRecipe", 0);
        }

        [Fact]
        public void Recipe_ConditionOnRetiredProficiency_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Synthesis)).ToList(),
                Proficiencies = [Proficiency(0, pathId: 0, maxLevel: 10, retiredAt: Retired)],
                SkillRecipes = [Recipe(0, resultSkillId: 6, inputs: [1], conditions: [(0, 1)])],
            };
            AssertHasFinding(graph, "RecipeCondition", ContentGraphSeverity.Warning, "SkillRecipe", 0);
        }

        [Fact]
        public void Recipe_ConditionAboveProficiencyMaxLevel_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Synthesis)).ToList(),
                SkillRecipes = [Recipe(0, resultSkillId: 6, inputs: [1], conditions: [(0, 999)])],
            };
            AssertHasFinding(graph, "RecipeCondition", ContentGraphSeverity.Warning, "SkillRecipe", 0);
        }

        [Fact]
        public void OrphanSkill_SynthesisFlaggedWithNoRecipe_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Synthesis)).ToList(),
            };
            AssertHasFinding(graph, "OrphanSkill", ContentGraphSeverity.Warning, "Skill", 6);
        }

        [Fact]
        public void OrphanSkill_EnemyFlaggedInNoPool_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                // Skill 2 is Enemy-flagged; drop it from every enemy pool.
                Enemies = [Enemy(0, skillPool: [], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            AssertHasFinding(graph, "OrphanSkill", ContentGraphSeverity.Warning, "Skill", 2);
        }

        [Fact]
        public void OrphanSkill_PlayerFlaggedGrantedByNothing_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Skills = HealthyGraph().Skills.Append(Skill(6, ESkillAcquisition.Player)).ToList(),
            };
            AssertHasFinding(graph, "OrphanSkill", ContentGraphSeverity.Warning, "Skill", 6);
        }

        [Fact]
        public void Zone_GatedByReachableEnemyTargetChallenge_IsReachable()
        {
            // Zone 1 gated by killing enemy 0, which spawns in the reachable start zone 0.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges = [Challenge(0, entityType: EEntityType.Enemy, targetEntityId: 0)],
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "ZoneReachability");
        }

        [Fact]
        public void Zone_GatedByReachableBossTargetChallenge_IsReachable()
        {
            // Zone 1 gated by defeating enemy 1, the boss of the reachable start zone 0.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0, bossEnemyId: 1), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges = [Challenge(0, entityType: EEntityType.Enemy, targetEntityId: 1)],
                Enemies = [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
            };
            Assert.DoesNotContain(_checker.Check(graph), f => f.Check == "ZoneReachability");
        }

        [Fact]
        public void Zone_GatedByUnreachableEnemyTargetChallenge_IsWarning()
        {
            // Zone 1 gated by killing enemy 3, which only spawns in the (unreachable) gated zone 1 itself.
            var graph = HealthyGraph() with
            {
                Zones = [Zone(0), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges = [Challenge(0, entityType: EEntityType.Enemy, targetEntityId: 3)],
                Enemies =
                [
                    Enemy(0, skillPool: [2], spawns: [(0, 1)]),
                    Enemy(1, isBoss: true),
                    Enemy(3, skillPool: [2], spawns: [(1, 1)]),
                ],
            };
            AssertHasFinding(graph, "ZoneReachability", ContentGraphSeverity.Warning, "Zone", 1);
        }

        // --- Lessons ----------------------------------------------------------------------------------

        [Fact]
        public void Lesson_MechanicEventTrigger_WithNoMechanicEvent_IsError()
        {
            var graph = HealthyGraph() with { Lessons = TaughtByBlurbLessons(critDodgeMechanicEvent: null) };
            AssertHasFinding(graph, "LessonTrigger", ContentGraphSeverity.Error, "Lesson", 0);
        }

        [Fact]
        public void Lesson_ScreenVisitTrigger_WithMechanicEvent_IsWarning()
        {
            var graph = HealthyGraph() with { Lessons = TaughtByBlurbLessons(idleLoopMechanicEvent: EMechanicEvent.FirstDodge) };
            AssertHasFinding(graph, "LessonTrigger", ContentGraphSeverity.Warning, "Lesson", 1);
        }

        [Fact]
        public void Lesson_BlankScreenKey_IsError()
        {
            var graph = HealthyGraph() with { Lessons = TaughtByBlurbLessons(idleLoopScreenKey: " ") };
            AssertHasFinding(graph, "LessonTrigger", ContentGraphSeverity.Error, "Lesson", 1);
        }

        [Fact]
        public void Lesson_MissingTaughtByBlurbCandidate_IsWarning()
        {
            // Drop the "idle-loop-basics" lesson: content-design.md §2 still names it as a taught-by-blurb
            // candidate, so its absence should surface rather than pass silently.
            var graph = HealthyGraph() with { Lessons = TaughtByBlurbLessons(includeIdleLoop: false) };
            AssertHasFinding(graph, "LessonCoverage", ContentGraphSeverity.Warning, "Lesson", -1);
        }

        [Fact]
        public void Lesson_RetiredCandidateLesson_StillCountsAsMissingCoverage()
        {
            // A retired lesson is out of circulation, so it no longer satisfies the candidate's coverage —
            // mirroring how a retired boss/challenge/etc. no longer satisfies the reference it once did.
            var graph = HealthyGraph() with { Lessons = TaughtByBlurbLessons(idleLoopRetiredAt: Retired) };
            AssertHasFinding(graph, "LessonCoverage", ContentGraphSeverity.Warning, "Lesson", -1);
        }

        [Fact]
        public void Lesson_DuplicateKey_IsWarning()
        {
            // A duplicate key would let the required-blurb coverage check match vacuously against either copy.
            var graph = HealthyGraph() with
            {
                Lessons = TaughtByBlurbLessons()
                    .Append(Lesson(3, "cooldown-charging", ELessonTriggerType.MechanicEvent, mechanicEvent: EMechanicEvent.FirstCooldownRecharge))
                    .ToList(),
            };
            AssertHasFinding(graph, "LessonKey", ContentGraphSeverity.Warning, "Lesson", 2);
            AssertHasFinding(graph, "LessonKey", ContentGraphSeverity.Warning, "Lesson", 3);
        }

        [Fact]
        public void Lesson_DuplicateOrdinal_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Lessons = TaughtByBlurbLessons()
                    .Append(Lesson(3, "extra-lesson", ELessonTriggerType.MechanicEvent, mechanicEvent: EMechanicEvent.FirstCooldownRecharge, ordinal: 2))
                    .ToList(),
            };
            AssertHasFinding(graph, "LessonOrdinal", ContentGraphSeverity.Warning, "Lesson", 2);
            AssertHasFinding(graph, "LessonOrdinal", ContentGraphSeverity.Warning, "Lesson", 3);
        }

        [Fact]
        public void Lesson_NonContiguousStepOrdinals_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Lessons = TaughtByBlurbLessons()
                    .Append(Lesson(3, "extra-lesson", ELessonTriggerType.MechanicEvent, mechanicEvent: EMechanicEvent.FirstCooldownRecharge, ordinal: 3,
                        steps:
                        [
                            new Contracts.LessonStep { Ordinal = 0, Text = "Step 0", AnchorKey = null },
                            new Contracts.LessonStep { Ordinal = 2, Text = "Step 2", AnchorKey = null },
                        ]))
                    .ToList(),
            };
            AssertHasFinding(graph, "LessonStepOrdinal", ContentGraphSeverity.Warning, "Lesson", 3);
        }

        [Fact]
        public void Lesson_EmptyStepText_IsWarning()
        {
            var graph = HealthyGraph() with
            {
                Lessons = TaughtByBlurbLessons()
                    .Append(Lesson(3, "extra-lesson", ELessonTriggerType.MechanicEvent, mechanicEvent: EMechanicEvent.FirstCooldownRecharge, ordinal: 3,
                        steps: [new Contracts.LessonStep { Ordinal = 0, Text = "   ", AnchorKey = null }]))
                    .ToList(),
            };
            AssertHasFinding(graph, "LessonStepText", ContentGraphSeverity.Warning, "Lesson", 3);
        }

        [Fact]
        public void Finding_ToString_IsHumanReadable()
        {
            var finding = new ContentGraphFinding(ContentGraphSeverity.Error, "ZoneBoss", "Zone", 3, "references enemy 9, which does not exist.");
            Assert.Equal("[Error] Zone 3 (ZoneBoss): references enemy 9, which does not exist.", finding.ToString());
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
                Tags: [],
                Items: [Item(0, grantedSkillId: 3)],
                ItemMods: [],
                Enemies: [Enemy(0, skillPool: [2], spawns: [(0, 1), (1, 1)]), Enemy(1, isBoss: true)],
                // Zone 0 carries a boss (enemy 1) so the default ZonesCleared challenge below can actually
                // clear it — a boss-less Zone target is itself a lint finding (ChallengeTarget).
                Zones: [Zone(0, bossEnemyId: 1), Zone(1, unlockChallengeId: 0), Zone(2, isHome: true)],
                Challenges: [Challenge(0, entityType: EEntityType.Zone, targetEntityId: 0)],
                Classes: [Class(0, starterSkills: [1])],
                Paths: [Path(0)],
                Proficiencies: [Proficiency(0, pathId: 0, maxLevel: 10, rewards: [(5, 5)])],
                SkillRecipes: [],
                // A healthy graph must also cover the taught-by-blurb candidates, or CheckLessons's coverage
                // warning would fire on every other test's otherwise-clean graph too.
                Lessons: TaughtByBlurbLessons());
        }

        private static Contracts.Skill Skill(
            int id,
            ESkillAcquisition acquisition,
            DateTime? retiredAt = null,
            EDamageType primaryType = EDamageType.Physical,
            (EAttribute attributeId, decimal multiplier)[]? damageMultipliers = null,
            (EAttribute attributeId, decimal scalingAmount)[]? effectScaling = null) => new()
            {
                Id = id,
                Name = $"Skill {id}",
                BaseDamage = 1,
                DamageMultipliers = (damageMultipliers ?? [])
                .Select(m => new Contracts.AttributeMultiplier { AttributeId = m.attributeId, Multiplier = m.multiplier })
                .ToList(),
                Effects = (effectScaling ?? [])
                .Select(e => new Contracts.SkillEffect
                {
                    Target = ESkillEffectTarget.Self,
                    AttributeId = EAttribute.MaxHealth,
                    DurationMs = 1000,
                    ScalingAttributeId = e.attributeId,
                    ScalingAmount = e.scalingAmount,
                })
                .ToList(),
                DamagePortions = [new Contracts.SkillDamagePortion { Type = primaryType, Weight = 1 }],
                Description = "",
                CooldownMs = 1000,
                IconPath = "",
                RarityId = ERarity.Common,
                Word = "",
                Pronunciation = "",
                Translation = "",
                Acquisition = acquisition,
                DesignerNotes = "",
                RetiredAt = retiredAt,
            };

        private static Contracts.Item Item(
            int id,
            EItemCategory category = EItemCategory.Accessory,
            int? grantedSkillId = null,
            EDamageType? weaponType = null,
            int? requiredProficiencyId = null,
            int requiredProficiencyLevel = 0,
            int[]? tags = null,
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
                Tags = tags ?? [],
                GrantedSkillId = grantedSkillId,
                WeaponType = weaponType,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                DesignerNotes = "",
                RetiredAt = retiredAt,
            };

        private static Contracts.ItemMod ItemMod(int id, int[]? tags = null, DateTime? retiredAt = null) => new()
        {
            Id = id,
            Name = $"ItemMod {id}",
            Description = "",
            ItemModTypeId = EItemModType.Prefix,
            RarityId = ERarity.Common,
            Attributes = [],
            Tags = tags ?? [],
            DesignerNotes = "",
            RetiredAt = retiredAt,
        };

        private static Contracts.Tag Tag(int id) => new() { Id = id, Name = $"Tag {id}", TagCategoryId = (int)ETagCategory.Accessory };

        private static Contracts.Enemy Enemy(
            int id,
            bool isBoss = false,
            int[]? skillPool = null,
            (int zoneId, int weight)[]? spawns = null,
            DateTime? retiredAt = null,
            (EAttribute attributeId, decimal baseAmount, decimal amountPerLevel)[]? attributeDistribution = null) => new()
            {
                Id = id,
                Name = $"Enemy {id}",
                IsBoss = isBoss,
                AttributeDistribution = (attributeDistribution ?? [])
                    .Select(d => new Contracts.AttributeDistribution { AttributeId = d.attributeId, BaseAmount = d.baseAmount, AmountPerLevel = d.amountPerLevel })
                    .ToList(),
                SkillPool = skillPool ?? [],
                Spawns = (spawns ?? []).Select(s => new Contracts.EnemySpawn { ZoneId = s.zoneId, Weight = s.weight }).ToList(),
                DesignerNotes = "",
                RetiredAt = retiredAt,
            };

        private static Contracts.Zone Zone(
            int id,
            bool isHome = false,
            int? bossEnemyId = null,
            int bossLevel = 1,
            int? unlockChallengeId = null,
            int? order = null,
            int levelMin = 1,
            int levelMax = 10,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = $"Zone {id}",
                Description = "",
                Order = order ?? id,
                LevelMin = levelMin,
                LevelMax = levelMax,
                BossEnemyId = bossEnemyId,
                BossLevel = bossLevel,
                UnlockChallengeId = unlockChallengeId,
                IsHome = isHome,
                DesignerNotes = "",
                RetiredAt = retiredAt,
            };

        private static Contracts.Challenge Challenge(
            int id,
            EEntityType entityType = EEntityType.None,
            int? targetEntityId = null,
            int? rewardItemId = null,
            int? rewardItemModId = null,
            DateTime? retiredAt = null,
            decimal progressGoal = 1,
            EChallengeType challengeTypeId = EChallengeType.ZonesCleared) => new()
            {
                Id = id,
                Name = $"Challenge {id}",
                Description = "",
                ChallengeTypeId = challengeTypeId,
                StatisticType = EStatisticType.ZonesCleared,
                EntityType = entityType,
                TargetEntityId = targetEntityId,
                ProgressGoal = progressGoal,
                RewardItemId = rewardItemId,
                RewardItemModId = rewardItemModId,
                DesignerNotes = "",
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
            DesignerNotes = "",
            RetiredAt = retiredAt,
        };

        private static Contracts.Path Path(int id, DateTime? retiredAt = null) => new()
        {
            Id = id,
            Name = $"Path {id}",
            Description = "",
            ActivityKey = default,
            DesignerNotes = "",
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
                DesignerNotes = "",
                RetiredAt = retiredAt,
                LevelModifiers = [],
                LevelRewards = (rewards ?? []).Select(r => new Contracts.ProficiencyLevelReward { Level = r.level, RewardSkillId = r.rewardSkillId }).ToList(),
                PrerequisiteIds = prerequisiteIds ?? [],
            };

        private static Contracts.SkillRecipe Recipe(int id, int resultSkillId, int[]? inputs = null, (int proficiencyId, int minLevel)[]? conditions = null, DateTime? retiredAt = null) => new()
        {
            Id = id,
            ResultSkillId = resultSkillId,
            DesignerNotes = "",
            RetiredAt = retiredAt,
            InputSkillIds = inputs ?? [],
            Conditions = (conditions ?? []).Select(c => new Contracts.SkillRecipeCondition { ProficiencyId = c.proficiencyId, MinLevel = c.minLevel }).ToList(),
        };

        private static Contracts.Lesson Lesson(
            int id,
            string key,
            ELessonTriggerType triggerType,
            EMechanicEvent? mechanicEvent = null,
            string screenKey = "fight",
            DateTime? retiredAt = null,
            int? ordinal = null,
            IEnumerable<Contracts.LessonStep>? steps = null) => new()
            {
                Id = id,
                Key = key,
                Name = $"Lesson {id}",
                TriggerType = triggerType,
                ScreenKey = screenKey,
                TriggerMechanicEvent = mechanicEvent,
                Ordinal = ordinal ?? id,
                DesignerNotes = "",
                RetiredAt = retiredAt,
                Steps = steps ?? [new Contracts.LessonStep { Ordinal = 0, Text = "Step", AnchorKey = null }],
            };

        /// <summary>The three taught-by-blurb candidates, healthy by default; a test overrides one entry to
        /// craft a specific violation.</summary>
        private static List<Contracts.Lesson> TaughtByBlurbLessons(
            EMechanicEvent? critDodgeMechanicEvent = EMechanicEvent.FirstCrit,
            ELessonTriggerType idleLoopTriggerType = ELessonTriggerType.ScreenVisit,
            EMechanicEvent? idleLoopMechanicEvent = null,
            string idleLoopScreenKey = "fight",
            DateTime? idleLoopRetiredAt = null,
            bool includeIdleLoop = true) =>
            [
                Lesson(0, "crit-dodge-variance", ELessonTriggerType.MechanicEvent, mechanicEvent: critDodgeMechanicEvent),
                .. includeIdleLoop
                    ? (Contracts.Lesson[])
                        [Lesson(1, "idle-loop-basics", idleLoopTriggerType, mechanicEvent: idleLoopMechanicEvent, screenKey: idleLoopScreenKey, retiredAt: idleLoopRetiredAt)]
                    : [],
                Lesson(2, "cooldown-charging", ELessonTriggerType.MechanicEvent, mechanicEvent: EMechanicEvent.FirstCooldownRecharge),
            ];
    }
}
