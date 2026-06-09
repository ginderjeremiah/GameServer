using System.Text;
using Game.Infrastructure.Entities;
using Game.Core;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.TestInfrastructure.Helpers
{
    public static class TestDataSeeder
    {
        private static readonly byte[] TestPepperBytes = Encoding.UTF8.GetBytes(TestAuthHelper.TestPepper);

        // Seeds a user whose credential is stored in the legacy (pre-PBKDF2) format. This is intentional:
        // it keeps coverage on the transparent login-migration path, and a legacy hash is still accepted
        // by the current Pbkdf2PasswordHasher (which upgrades it on first login).
        public static async Task<User> CreateUserAsync(GameContext context, string username = "testuser", string password = "testpass")
        {
            var salt = Guid.NewGuid();
            var passHash = LegacyPasswordHash.Hash(password, salt.ToString(), TestPepperBytes);

            var user = new User
            {
                Username = username,
                Salt = salt,
                PassHash = passHash,
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
            int cooldownMs = 1000)
        {
            var skill = new Skill
            {
                Name = name,
                Description = "",
                BaseDamage = baseDamage,
                CooldownMs = cooldownMs,
                IconPath = "",
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
            int? unlockChallengeId = null)
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

        public static async Task<Challenge> CreateChallengeAsync(
            GameContext context,
            string name = "Test Challenge",
            EChallengeType challengeTypeId = EChallengeType.EnemiesKilled,
            decimal progressGoal = 10m,
            int? targetEntityId = null,
            int? rewardItemId = null,
            int? rewardItemModId = null)
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

        public static async Task<Enemy> CreateStrongEnemyAsync(GameContext context)
        {
            return await CreateEnemyAsync(context, "Strong Enemy",
                strengthBase: 200m, strengthPerLevel: 0m,
                enduranceBase: 200m, endurancePerLevel: 0m);
        }
    }
}
