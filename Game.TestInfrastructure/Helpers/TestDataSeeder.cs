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
            int zoneId = 0,
            int? classId = null)
        {
            // Every player references a resolvable class — battle assembly composes the class locked base from
            // it (#1223). Reuse the caller-supplied class, or seed a minimal one with no attribute distributions
            // (an empty locked base), so a directly-seeded player's battle attributes stay exactly its stat
            // allocations + gear and existing reward/exp expectations are unaffected.
            var resolvedClassId = classId ?? (await CreateClassAsync(context)).Id;

            var player = new Player
            {
                UserId = userId,
                ClassId = resolvedClassId,
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
            ERarity rarity = ERarity.Common,
            EDamageType damageType = EDamageType.Physical,
            string word = "",
            string pronunciation = "",
            string translation = "")
        {
            var skill = new Skill
            {
                Name = name,
                Description = "",
                BaseDamage = baseDamage,
                CooldownMs = cooldownMs,
                IconPath = "",
                RarityId = (int)rarity,
                Word = word,
                Pronunciation = pronunciation,
                Translation = translation,
                Acquisition = (int)acquisition,
                DesignerNotes = "",
            };

            context.Skills.Add(skill);
            await context.SaveChangesAsync();

            // Seed a single full-weight portion of the requested type (the backfilled single-portion shape).
            context.SkillDamagePortions.Add(new SkillDamagePortion
            {
                SkillId = skill.Id,
                DamageType = (int)damageType,
                Weight = 1.0m,
            });
            context.SkillDamageMultipliers.Add(new SkillDamageMultiplier
            {
                SkillId = skill.Id,
                AttributeId = (int)EAttribute.Strength,
                Multiplier = 1.0m,
            });
            await context.SaveChangesAsync();

            return skill;
        }

        /// <summary>
        /// Seeds a skill-synthesis recipe producing <paramref name="resultSkillId"/> from the given
        /// <paramref name="inputSkillIds"/>, with optional proficiency-level conditions.
        /// </summary>
        public static async Task<SkillRecipe> CreateSkillRecipeAsync(
            GameContext context,
            int resultSkillId,
            IEnumerable<int> inputSkillIds,
            IEnumerable<(int ProficiencyId, int MinLevel)>? conditions = null)
        {
            var recipe = new SkillRecipe { ResultSkillId = resultSkillId, DesignerNotes = "" };
            context.SkillRecipes.Add(recipe);
            await context.SaveChangesAsync();

            context.SkillRecipeInputs.AddRange(inputSkillIds.Select(skillId =>
                new SkillRecipeInput { RecipeId = recipe.Id, SkillId = skillId }));
            context.SkillRecipeConditions.AddRange((conditions ?? []).Select(c =>
                new SkillRecipeCondition { RecipeId = recipe.Id, ProficiencyId = c.ProficiencyId, MinLevel = c.MinLevel }));
            await context.SaveChangesAsync();

            return recipe;
        }

        public static async Task<Item> CreateItemAsync(
            GameContext context,
            string name = "Test Item",
            EAttribute attributeId = EAttribute.Strength,
            decimal attributeAmount = 5m,
            EItemCategory category = EItemCategory.Weapon,
            int? grantedSkillId = null,
            int? requiredProficiencyId = null,
            int requiredProficiencyLevel = 0)
        {
            var item = new Item
            {
                Name = name,
                Description = "",
                ItemCategoryId = (int)category,
                RarityId = (int)ERarity.Common,
                IconPath = "",
                GrantedSkillId = grantedSkillId,
                RequiredProficiencyId = requiredProficiencyId,
                RequiredProficiencyLevel = requiredProficiencyLevel,
                DesignerNotes = "",
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
                DesignerNotes = "",
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
            var enemy = new Enemy { Name = name, IsBoss = isBoss, DesignerNotes = "" };
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
            bool isHome = false,
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
                IsHome = isHome,
                RetiredAt = retiredAt,
                DesignerNotes = "",
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

        // Routes the given skill's damage type to the proficiency's path, so the skill's direct-hit damage in a
        // won battle trains that path's frontier tier (the effect-based accrual, spike #1318). Aligns the path's
        // ActivityKey with the skill's leaf damage-type key.
        public static async Task LinkSkillToProficiencyAsync(GameContext context, int proficiencyId, int skillId)
        {
            var proficiency = await context.Proficiencies.FindAsync(proficiencyId)
                ?? throw new InvalidOperationException($"Proficiency {proficiencyId} has not been seeded.");
            var path = await context.Paths.FindAsync(proficiency.PathId)
                ?? throw new InvalidOperationException($"Path {proficiency.PathId} has not been seeded.");

            // Route the skill's primary (highest-weight) portion type to the path so a won battle's direct-hit
            // damage trains that path's frontier tier (the effect-based accrual, spike #1318).
            var portions = await context.SkillDamagePortions
                .Where(p => p.SkillId == skillId)
                .ToListAsync();
            if (portions.Count == 0)
            {
                throw new InvalidOperationException($"Skill {skillId} has not been seeded.");
            }
            var primaryType = portions.MaxBy(p => p.Weight)?.DamageType ?? (int)EDamageType.Physical;
            var leafKey = Game.Core.Attributes.DamageTypes.Applies((EDamageType)primaryType)[0];
            path.ActivityKey = (int)Game.Core.Proficiencies.ActivityKeys.ForDamageKey(leafKey);
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
                DesignerNotes = "",
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

        public static async Task<Class> CreateClassAsync(
            GameContext context,
            string name = "Test Class",
            string word = "aenkor",
            EAttribute passiveAttribute = EAttribute.Strength,
            decimal passiveAmount = 5m,
            EAttribute? passiveScalingAttribute = null,
            decimal passiveScalingAmount = 0m,
            EModifierType passiveModifierType = EModifierType.Additive,
            DateTime? retiredAt = null)
        {
            var @class = new Class
            {
                Name = name,
                Description = "",
                Word = word,
                PassiveAttributeId = (int)passiveAttribute,
                PassiveAmount = passiveAmount,
                PassiveScalingAttributeId = (int?)passiveScalingAttribute,
                PassiveScalingAmount = passiveScalingAmount,
                PassiveModifierType = (int)passiveModifierType,
                DesignerNotes = "",
                RetiredAt = retiredAt,
            };

            context.Classes.Add(@class);
            await context.SaveChangesAsync();
            return @class;
        }

        /// <summary>Seeds a class with a kit: the given starter skills, optional starter equipment (each
        /// <c>{ ItemId, EquipmentSlot }</c>, equipped at creation), and an optional attribute distribution
        /// (each <c>{ Attribute, BaseAmount, AmountPerLevel }</c>). The referenced skills/items must already
        /// exist.</summary>
        public static async Task<Class> CreateClassWithKitAsync(
            GameContext context,
            IReadOnlyList<int> starterSkillIds,
            IReadOnlyList<(EAttribute Attribute, decimal BaseAmount, decimal AmountPerLevel)>? attributeDistributions = null,
            string name = "Test Class",
            DateTime? retiredAt = null,
            IReadOnlyList<(int ItemId, EEquipmentSlot Slot)>? starterEquipment = null,
            EAttribute passiveAttribute = EAttribute.Strength,
            decimal passiveAmount = 5m,
            EAttribute? passiveScalingAttribute = null,
            decimal passiveScalingAmount = 0m,
            EModifierType passiveModifierType = EModifierType.Additive)
        {
            var @class = await CreateClassAsync(
                context,
                name: name,
                retiredAt: retiredAt,
                passiveAttribute: passiveAttribute,
                passiveAmount: passiveAmount,
                passiveScalingAttribute: passiveScalingAttribute,
                passiveScalingAmount: passiveScalingAmount,
                passiveModifierType: passiveModifierType);

            foreach (var skillId in starterSkillIds)
            {
                context.ClassStarterSkills.Add(new ClassStarterSkill { ClassId = @class.Id, SkillId = skillId });
            }

            foreach (var (itemId, slot) in starterEquipment ?? [])
            {
                context.ClassStarterEquipment.Add(new ClassStarterEquipment
                {
                    ClassId = @class.Id,
                    ItemId = itemId,
                    EquipmentSlotId = (int)slot,
                });
            }

            foreach (var (attribute, baseAmount, amountPerLevel) in attributeDistributions ?? [])
            {
                context.ClassAttributeDistributions.Add(new ClassAttributeDistribution
                {
                    ClassId = @class.Id,
                    AttributeId = (int)attribute,
                    BaseAmount = baseAmount,
                    AmountPerLevel = amountPerLevel,
                });
            }

            await context.SaveChangesAsync();
            return @class;
        }

        /// <summary>The standard character-creation fixture: seeds skills <c>0..starterSkillCount-1</c> and a
        /// class whose kit is exactly those skills with a uniform <paramref name="coreAttributeBase"/> spread
        /// across the six core attributes (ids 0-5), so a created player's starter graph is fully determined.
        /// Returns the class (its zero-based <c>Id</c> is the class to create characters as).</summary>
        public static async Task<Class> CreateStandardCreatableClassAsync(
            GameContext context,
            int starterSkillCount = 3,
            decimal coreAttributeBase = 5m)
        {
            var starterSkillIds = new List<int>();
            for (var i = 0; i < starterSkillCount; i++)
            {
                starterSkillIds.Add((await CreateSkillAsync(context, $"Skill{i}")).Id);
            }

            var distributions = Enumerable.Range(0, 6)
                .Select(id => ((EAttribute)id, coreAttributeBase, 0m))
                .ToList();

            return await CreateClassWithKitAsync(context, starterSkillIds, distributions);
        }

        public static async Task<Path> CreatePathAsync(
            GameContext context,
            string name = "Test Path",
            EActivityKey activityKey = EActivityKey.Physical,
            DateTime? retiredAt = null)
        {
            var path = new Path
            {
                Name = name,
                Description = "",
                ActivityKey = (int)activityKey,
                RetiredAt = retiredAt,
                DesignerNotes = "",
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
            string word = "",
            string pronunciation = "",
            string translation = "")
        {
            pathId ??= (await CreatePathAsync(context)).Id;

            var proficiency = new Proficiency
            {
                Name = name,
                Description = "",
                IconPath = "",
                Word = word,
                Pronunciation = pronunciation,
                Translation = translation,
                PathId = pathId.Value,
                PathOrdinal = pathOrdinal,
                MaxLevel = maxLevel,
                BaseXp = baseXp,
                XpGrowth = xpGrowth,
                DesignerNotes = "",
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
