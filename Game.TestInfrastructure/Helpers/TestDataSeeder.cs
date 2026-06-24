using Game.Abstractions.Auth;
using Game.Application.Auth;
using Game.Infrastructure.Entities;
using Game.Core;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Path = Game.Infrastructure.Entities.Path;

namespace Game.TestInfrastructure.Helpers
{
    public static class TestDataSeeder
    {
        // A real PBKDF2 hasher matching the pepper used across the integration tests, with a cheap
        // iteration count so seeding stays fast. Shared so every seeded credential is in the current
        // self-contained format the production hasher produces.
        private static readonly IPasswordHasher PasswordHasher = new Pbkdf2PasswordHasher(
            Options.Create(new PasswordHashingOptions
            {
                Pepper = TestAuthHelper.TestPepper,
                Iterations = 1000,
            }));

        // Seeds a user whose credential is stored in the current self-contained PBKDF2 format.
        public static async Task<User> CreateUserAsync(GameContext context, string username = "testuser", string password = "testpass")
        {
            var user = new User
            {
                Username = username,
                PassHash = PasswordHasher.Hash(password),
                LastLogin = DateTime.UtcNow,
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        public static async Task<Player> CreatePlayerAsync(
            GameContext context,
            int userId,
            string name = "TestPlayer",
            int level = 5,
            int zoneId = 0)
        {
            var player = new Player
            {
                UserId = userId,
                Name = name,
                Level = level,
                Exp = 0,
                CurrentZoneId = zoneId,
                StatPointsGained = 100,
                StatPointsUsed = 100,
                LastActivity = DateTime.UtcNow,
            };

            context.Players.Add(player);
            await context.SaveChangesAsync();

            context.PlayerAttributes.AddRange(
                new PlayerAttribute { PlayerId = player.Id, AttributeId = (int)EAttribute.Strength, Amount = 50m },
                new PlayerAttribute { PlayerId = player.Id, AttributeId = (int)EAttribute.Endurance, Amount = 50m });
            await context.SaveChangesAsync();

            return player;
        }

        public static async Task<Skill> CreateSkillAsync(
            GameContext context,
            string name = "Attack",
            decimal baseDamage = 10m,
            int cooldownMs = 1000,
            ESkillAcquisition acquisition = ESkillAcquisition.Player,
            ERarity rarity = ERarity.Common)
        {
            var skill = new Skill
            {
                Name = name,
                Description = "",
                BaseDamage = baseDamage,
                CooldownMs = cooldownMs,
                IconPath = "",
                RarityId = (int)rarity,
                Acquisition = (int)acquisition,
            };

            context.Skills.Add(skill);
            await context.SaveChangesAsync();

            context.SkillDamageMultipliers.Add(new SkillDamageMultiplier
            {
                SkillId = skill.Id,
                AttributeId = (int)EAttribute.Strength,
                Multiplier = 1.0m,
            });
            await context.SaveChangesAsync();

            return skill;
        }

        public static async Task<Item> CreateItemAsync(
            GameContext context,
            string name = "Test Item",
            EAttribute attributeId = EAttribute.Strength,
            decimal attributeAmount = 5m)
        {
            var item = new Item
            {
                Name = name,
                Description = "",
                ItemCategoryId = (int)EItemCategory.Weapon,
                RarityId = (int)ERarity.Common,
                IconPath = "",
            };

            context.Items.Add(item);
            await context.SaveChangesAsync();

            context.ItemAttributes.Add(new ItemAttribute
            {
                ItemId = item.Id,
                AttributeId = (int)attributeId,
                Amount = attributeAmount,
            });
            await context.SaveChangesAsync();

            return item;
        }

        public static async Task<ItemMod> CreateItemModAsync(
            GameContext context,
            string name = "Test Mod",
            EAttribute attributeId = EAttribute.Strength,
            ERarity rarityId = ERarity.Common,
            decimal attributeAmount = 5m)
        {
            var itemMod = new ItemMod
            {
                Name = name,
                Description = "",
                ItemModTypeId = (int)EItemModType.Prefix,
                RarityId = (int)rarityId,
            };

            context.ItemMods.Add(itemMod);
            await context.SaveChangesAsync();

            context.ItemModAttributes.Add(new ItemModAttribute
            {
                ItemModId = itemMod.Id,
                AttributeId = (int)attributeId,
                Amount = attributeAmount,
            });
            await context.SaveChangesAsync();

            return itemMod;
        }

        // TagCategories are intrinsic, migration-seeded reference data (preserved across test
        // truncation), so tags reference an existing seeded category rather than creating one.
        public static async Task<Tag> CreateTagAsync(
            GameContext context,
            string name = "Test Tag",
            ETagCategory category = ETagCategory.Accessory)
        {
            var tag = new Tag { Name = name, TagCategoryId = (int)category };
            context.Tags.Add(tag);
            await context.SaveChangesAsync();
            return tag;
        }

        public static async Task<Enemy> CreateEnemyAsync(
            GameContext context,
            string name = "Test Enemy",
            decimal strengthBase = 5m,
            decimal strengthPerLevel = 1m,
            decimal enduranceBase = 5m,
            decimal endurancePerLevel = 1m,
            bool isBoss = false)
        {
            var enemy = new Enemy { Name = name, IsBoss = isBoss };
            context.Enemies.Add(enemy);
            await context.SaveChangesAsync();

            context.AttributeDistributions.AddRange(
                new AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = (int)EAttribute.Strength,
                    BaseAmount = strengthBase,
                    AmountPerLevel = strengthPerLevel,
                },
                new AttributeDistribution
                {
                    EnemyId = enemy.Id,
                    AttributeId = (int)EAttribute.Endurance,
                    BaseAmount = enduranceBase,
                    AmountPerLevel = endurancePerLevel,
                });
            await context.SaveChangesAsync();

            return enemy;
        }

        public static async Task<Zone> CreateZoneAsync(
            GameContext context,
            string name = "Test Zone",
            int levelMin = 1,
            int levelMax = 10,
            int order = 0,
            int? bossEnemyId = null,
            int bossLevel = 1,
            int? unlockChallengeId = null,
            DateTime? retiredAt = null)
        {
            var zone = new Zone
            {
                Name = name,
                Description = "",
                Order = order,
                LevelMin = levelMin,
                LevelMax = levelMax,
                BossEnemyId = bossEnemyId,
                BossLevel = bossLevel,
                UnlockChallengeId = unlockChallengeId,
                RetiredAt = retiredAt,
            };

            context.Zones.Add(zone);
            await context.SaveChangesAsync();
            return zone;
        }

        public static async Task AssignRoleToUserAsync(GameContext context, int userId, ERole role)
        {
            var user = await context.Users.Include(u => u.Roles).FirstAsync(u => u.Id == userId);
            var roleEntity = await context.Roles.FirstAsync(r => r.Id == (int)role);
            user.Roles.Add(roleEntity);
            await context.SaveChangesAsync();
        }

        public static async Task LinkEnemyToZoneAsync(GameContext context, int zoneId, int enemyId, int weight = 1)
        {
            context.Set<ZoneEnemy>().Add(new ZoneEnemy { ZoneId = zoneId, EnemyId = enemyId, Weight = weight });
            await context.SaveChangesAsync();
        }

        public static async Task LinkSkillToEnemyAsync(GameContext context, int enemyId, int skillId)
        {
            context.EnemySkills.Add(new EnemySkill { EnemyId = enemyId, SkillId = skillId });
            await context.SaveChangesAsync();
        }

        // Adds a skill-to-path contribution (the join the battle XP accrual reads) homed at the given
        // proficiency's tier: firing skillId in a won battle feeds proficiencyId's XP, weighted by weight.
        public static async Task LinkSkillToProficiencyAsync(GameContext context, int proficiencyId, int skillId, decimal weight = 1m)
        {
            var proficiency = await context.Proficiencies.FindAsync(proficiencyId)
                ?? throw new InvalidOperationException($"Proficiency {proficiencyId} has not been seeded.");

            context.Set<SkillPathContribution>().Add(new SkillPathContribution
            {
                PathId = proficiency.PathId,
                HomeTier = proficiency.PathOrdinal,
                SkillId = skillId,
                Weight = weight,
            });
            await context.SaveChangesAsync();
        }

        public static async Task LinkSkillToPlayerAsync(GameContext context, int playerId, int skillId, bool selected = true, int order = 0)
        {
            context.PlayerSkills.Add(new PlayerSkill
            {
                PlayerId = playerId,
                SkillId = skillId,
                Selected = selected,
                Order = order,
            });
            await context.SaveChangesAsync();
        }

        // Marks an item as unlocked for a player, optionally already equipped in a slot and/or favorited.
        public static async Task LinkItemToPlayerAsync(
            GameContext context,
            int playerId,
            int itemId,
            EEquipmentSlot? equipmentSlot = null,
            bool favorite = false)
        {
            context.UnlockedItems.Add(new UnlockedItem
            {
                PlayerId = playerId,
                ItemId = itemId,
                EquipmentSlotId = (int?)equipmentSlot,
                Favorite = favorite,
            });
            await context.SaveChangesAsync();
        }

        // Authors a mod slot on an item and returns it so callers can resolve its store-generated Id.
        public static async Task<ItemModSlot> AddItemModSlotAsync(
            GameContext context,
            int itemId,
            EItemModType modType = EItemModType.Prefix)
        {
            var modSlot = new ItemModSlot
            {
                ItemId = itemId,
                ItemModSlotTypeId = (int)modType,
            };
            context.ItemModSlots.Add(modSlot);
            await context.SaveChangesAsync();
            return modSlot;
        }

        public static async Task LinkModToPlayerAsync(GameContext context, int playerId, int itemModId)
        {
            context.UnlockedMods.Add(new UnlockedMod
            {
                PlayerId = playerId,
                ItemModId = itemModId,
            });
            await context.SaveChangesAsync();
        }

        // Persists an applied mod as if it had been applied in a previous session.
        public static async Task ApplyModToItemAsync(
            GameContext context,
            int playerId,
            int itemId,
            int itemModSlotId,
            int itemModId)
        {
            context.AppliedMods.Add(new AppliedMod
            {
                PlayerId = playerId,
                ItemId = itemId,
                ItemModSlotId = itemModSlotId,
                ItemModId = itemModId,
            });
            await context.SaveChangesAsync();
        }

        public static async Task<Challenge> CreateChallengeAsync(
            GameContext context,
            string name = "Test Challenge",
            EChallengeType challengeTypeId = EChallengeType.EnemiesKilled,
            decimal progressGoal = 10m,
            int? targetEntityId = null,
            int? rewardItemId = null,
            int? rewardItemModId = null,
            DateTime? retiredAt = null)
        {
            var challenge = new Challenge
            {
                Name = name,
                Description = "",
                ChallengeTypeId = (int)challengeTypeId,
                ProgressGoal = progressGoal,
                TargetEntityId = targetEntityId,
                RewardItemId = rewardItemId,
                RewardItemModId = rewardItemModId,
                RetiredAt = retiredAt,
            };

            context.Challenges.Add(challenge);
            await context.SaveChangesAsync();
            return challenge;
        }

        public static async Task AddPlayerStatisticAsync(
            GameContext context,
            int playerId,
            EStatisticType statisticType,
            decimal value,
            int? entityId = null)
        {
            context.PlayerStatistics.Add(new PlayerStatistic
            {
                PlayerId = playerId,
                StatisticTypeId = (int)statisticType,
                EntityId = entityId,
                Value = value,
            });
            await context.SaveChangesAsync();
        }

        public static async Task AddPlayerChallengeAsync(
            GameContext context,
            int playerId,
            int challengeId,
            decimal progress = 0m,
            bool completed = false,
            DateTime? completedAt = null)
        {
            context.PlayerChallenges.Add(new PlayerChallenge
            {
                PlayerId = playerId,
                ChallengeId = challengeId,
                Progress = progress,
                Completed = completed,
                CompletedAt = completedAt,
            });
            await context.SaveChangesAsync();
        }

        public static async Task<Path> CreatePathAsync(
            GameContext context,
            string name = "Test Path",
            decimal falloffBase = 0.3m)
        {
            var path = new Path
            {
                Name = name,
                Description = "",
                FalloffBase = falloffBase,
            };

            context.Paths.Add(path);
            await context.SaveChangesAsync();
            return path;
        }

        // A proficiency is a tier of a path. When no path is supplied, a fresh standalone single-tier path is
        // created so the proficiency is always well-formed.
        public static async Task<Proficiency> CreateProficiencyAsync(
            GameContext context,
            string name = "Test Proficiency",
            int maxLevel = 10,
            decimal baseXp = 100m,
            decimal xpGrowth = 2m,
            int? pathId = null,
            int pathOrdinal = 0,
            bool startsUnlocked = true,
            int? seedSkillId = null)
        {
            pathId ??= (await CreatePathAsync(context)).Id;

            var proficiency = new Proficiency
            {
                Name = name,
                Description = "",
                IconPath = "",
                PathId = pathId.Value,
                PathOrdinal = pathOrdinal,
                MaxLevel = maxLevel,
                BaseXp = baseXp,
                XpGrowth = xpGrowth,
                StartsUnlocked = startsUnlocked,
                SeedSkillId = seedSkillId,
                LevelModifiers = [],
                LevelRewards = [],
                Prerequisites = [],
            };

            context.Proficiencies.Add(proficiency);
            await context.SaveChangesAsync();
            return proficiency;
        }

        // Authors a milestone reward: reaching the given level in the proficiency grants the reward skill.
        public static async Task AddProficiencyLevelRewardAsync(
            GameContext context, int proficiencyId, int level, int rewardSkillId)
        {
            context.Set<ProficiencyLevelReward>().Add(new ProficiencyLevelReward
            {
                ProficiencyId = proficiencyId,
                Level = level,
                RewardSkillId = rewardSkillId,
            });
            await context.SaveChangesAsync();
        }

        // Adds a cross-path gateway edge: the proficiency opens once prerequisiteId is maxed.
        public static async Task AddProficiencyPrerequisiteAsync(
            GameContext context, int proficiencyId, int prerequisiteId)
        {
            context.Set<ProficiencyPrerequisite>().Add(new ProficiencyPrerequisite
            {
                ProficiencyId = proficiencyId,
                PrerequisiteProficiencyId = prerequisiteId,
            });
            await context.SaveChangesAsync();
        }

        public static async Task AddPlayerProficiencyAsync(
            GameContext context,
            int playerId,
            int proficiencyId,
            int level = 0,
            decimal xp = 0m)
        {
            context.PlayerProficiencies.Add(new PlayerProficiency
            {
                PlayerId = playerId,
                ProficiencyId = proficiencyId,
                Level = level,
                Xp = xp,
            });
            await context.SaveChangesAsync();
        }

        // LogTypes are intrinsic, migration-seeded reference data, so the preference references an
        // existing seeded log type rather than creating one.
        public static async Task AddLogPreferenceAsync(
            GameContext context,
            int playerId,
            ELogType logType,
            bool enabled)
        {
            context.LogPreferences.Add(new LogPreference
            {
                PlayerId = playerId,
                LogTypeId = (int)logType,
                Enabled = enabled,
            });
            await context.SaveChangesAsync();
        }

        public static async Task<Enemy> CreateStrongEnemyAsync(GameContext context)
        {
            return await CreateEnemyAsync(context, "Strong Enemy",
                strengthBase: 200m, strengthPerLevel: 0m,
                enduranceBase: 200m, endurancePerLevel: 0m);
        }
    }
}
