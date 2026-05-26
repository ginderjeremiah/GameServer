using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Enemies;
using Game.Api.Models.Items;
using Game.Api.Models.Skills;
using Game.Api.Models.Tags;
using Game.Api.Models.Zones;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers
{
    [Route("/api/[controller]/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminToolsController(
        IEnemies enemies,
        IItems items,
        IItemMods itemMods,
        ISkills skills,
        ITags tags,
        IZones zones,
        IEntityStore entityStore) : ControllerBase
    {
        private readonly IEnemies _enemies = enemies;
        private readonly IItems _items = items;
        private readonly IItemMods _itemMods = itemMods;
        private readonly ISkills _skills = skills;
        private readonly ITags _tags = tags;
        private readonly IZones _zones = zones;
        private readonly IEntityStore _entityStore = entityStore;

        [HttpPost]
        public ApiResponse AddEditEnemies([FromBody] List<Change<Enemy>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.Enemy
                    {
                        Name = change.Item.Name
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var enemy = _enemies.GetEnemy(change.Item.Id);
                    if (enemy is not null)
                    {
                        enemy.Name = change.Item.Name;
                        _entityStore.Update(enemy);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var enemy = _enemies.GetEnemy(change.Item.Id);
                    if (enemy is not null)
                    {
                        _entityStore.Delete(enemy);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemAttributes([FromBody] AddEditAttributesData changeData)
        {
            var item = _items.LookupItem(changeData.Id);
            if (item is null)
            {
                return ApiResponse.Error("Item does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    item.ItemAttributes.Add(new Game.Abstractions.Entities.ItemAttribute()
                    {
                        ItemId = item.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var att = item.ItemAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        att.Amount = change.Item.Amount;
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var att = item.ItemAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        item.ItemAttributes.Remove(att);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemModAttributes([FromBody] AddEditAttributesData changeData)
        {
            var itemMod = _itemMods.LookupItemMod(changeData.Id);
            if (itemMod is null)
            {
                return ApiResponse.Error("Item Mod does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    itemMod.ItemModAttributes.Add(new Game.Abstractions.Entities.ItemModAttribute()
                    {
                        ItemModId = itemMod.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var att = itemMod.ItemModAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        att.Amount = change.Item.Amount;
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var att = itemMod.ItemModAttributes.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        itemMod.ItemModAttributes.Remove(att);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.ItemMod
                    {
                        Name = change.Item.Name,
                        Removable = change.Item.Removable,
                        Description = change.Item.Description,
                        ItemModTypeId = change.Item.ItemModTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var itemMod = _itemMods.LookupItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        itemMod.Name = change.Item.Name;
                        itemMod.Removable = change.Item.Removable;
                        itemMod.Description = change.Item.Description;
                        itemMod.ItemModTypeId = change.Item.ItemModTypeId;
                        _entityStore.Update(itemMod);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var itemMod = _itemMods.LookupItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        _entityStore.Delete(itemMod);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItems([FromBody] List<Change<Item>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.Item
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemCategoryId = (int)change.Item.ItemCategoryId,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var item = _items.LookupItem(change.Item.Id);
                    if (item is not null)
                    {
                        item.Name = change.Item.Name;
                        item.Description = change.Item.Description;
                        item.ItemCategoryId = (int)change.Item.ItemCategoryId;
                        item.IconPath = change.Item.IconPath;
                        _entityStore.Update(item);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var item = _items.LookupItem(change.Item.Id);
                    if (item is not null)
                    {
                        _entityStore.Delete(item);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditItemModSlots([FromBody] List<Change<ItemModSlot>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.ItemModSlot
                    {
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Game.Abstractions.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Game.Abstractions.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditSkills([FromBody] List<Change<Skill>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.Skill
                    {
                        Name = change.Item.Name,
                        BaseDamage = change.Item.BaseDamage,
                        CooldownMs = change.Item.CooldownMs,
                        Description = change.Item.Description,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Game.Abstractions.Entities.Skill
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        BaseDamage = change.Item.BaseDamage,
                        CooldownMs = change.Item.CooldownMs,
                        Description = change.Item.Description,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Game.Abstractions.Entities.Skill
                    {
                        Id = change.Item.Id,
                        Name = "",
                        Description = "",
                        IconPath = "",
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.Tag
                    {
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Game.Abstractions.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Game.Abstractions.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = "",
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse AddEditZones([FromBody] List<Change<Zone>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.Zone
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        LevelMin = change.Item.LevelMin,
                        LevelMax = change.Item.LevelMax,
                        Order = change.Item.Order,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Game.Abstractions.Entities.Zone
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        LevelMin = change.Item.LevelMin,
                        LevelMax = change.Item.LevelMax,
                        Order = change.Item.Order,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Game.Abstractions.Entities.Zone
                    {
                        Id = change.Item.Id,
                        Name = "",
                        Description = "",
                    });
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public ApiResponse SetEnemyAttributeDistributions([FromBody] SetEnemyAttributeDistributions distributionsData)
        {
            var enemy = _enemies.GetEnemy(distributionsData.EnemyId);
            if (enemy is not null)
            {
                var newIds = distributionsData.AttributeDistributions.Select(ad => (int)ad.AttributeId).ToList();
                foreach (var dist in enemy.AttributeDistributions.Where(ad => !newIds.Contains(ad.AttributeId)))
                {
                    _entityStore.Delete(dist);
                }

                foreach (var dist in enemy.AttributeDistributions.Where(ad => newIds.Contains(ad.AttributeId)))
                {
                    var newData = distributionsData.AttributeDistributions.First(ad => (int)ad.AttributeId == dist.AttributeId);
                    _entityStore.Update(new Game.Abstractions.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = dist.AttributeId,
                        BaseAmount = newData.AmountPerLevel,
                        AmountPerLevel = newData.AmountPerLevel
                    });
                }

                var existingIds = enemy.AttributeDistributions.Select(ad => ad.AttributeId).ToList();
                var newDistributions = distributionsData.AttributeDistributions
                    .Where(ad => !existingIds.Contains((int)ad.AttributeId))
                    .Select(ad => new Game.Abstractions.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = (int)ad.AttributeId,
                        BaseAmount = ad.AmountPerLevel,
                        AmountPerLevel = ad.AmountPerLevel
                    }).ToList();

                _entityStore.InsertAll(newDistributions);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetEnemySkills([FromBody] SetEnemySkillsData enemySkillsData)
        {
            var enemy = _enemies.GetEnemy(enemySkillsData.EnemyId);
            if (enemy is not null)
            {
                var newIds = enemySkillsData.SkillIds;
                foreach (var skill in enemy.EnemySkills.Where(e => !newIds.Contains(e.SkillId)))
                {
                    _entityStore.Delete(skill);
                }

                var existingIds = enemy.EnemySkills.Select(ze => ze.SkillId).ToList();
                var enemySkills = enemySkillsData.SkillIds
                    .Where(id => !existingIds.Contains(id))
                    .Select(id => new Game.Abstractions.Entities.EnemySkill
                    {
                        EnemyId = enemy.Id,
                        SkillId = id,
                    }).ToList();

                _entityStore.InsertAll(enemySkills);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Enemy not found.");
        }

        [HttpPost]
        public ApiResponse SetSkillMultipliers([FromBody] AddEditAttributesData changeData)
        {
            var skill = _skills.LookupSkill(changeData.Id);
            if (skill is null)
            {
                return ApiResponse.Error("Skill does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _entityStore.Insert(new Game.Abstractions.Entities.SkillDamageMultiplier
                    {
                        SkillId = skill.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Multiplier = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var att = skill.SkillDamageMultipliers.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        att.Multiplier = change.Item.Amount;
                        _entityStore.Update(att);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var att = skill.SkillDamageMultipliers.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        skill.SkillDamageMultipliers.Remove(att);
                        _entityStore.Delete(att);
                    }
                }
            }

            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            var item = _items.LookupItem(setTagsData.Id);
            if (item is not null)
            {
                item.Tags.Clear();
                var tags = _tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    item.Tags.Add(tag);
                }

                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            var itemMod = _itemMods.LookupItemMod(setTagsData.Id);
            if (itemMod is not null)
            {
                itemMod.Tags.Clear();
                var tags = _tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    itemMod.Tags.Add(tag);
                }

                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item mod not found.");
        }

        [HttpPost]
        public ApiResponse SetZoneEnemies([FromBody] SetZoneEnemiesData zoneEnemiesData)
        {
            var zone = _zones.GetZone(zoneEnemiesData.ZoneId);
            if (zone is not null)
            {
                var newIds = zoneEnemiesData.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
                foreach (var enemy in zone.ZoneEnemies.Where(e => !newIds.Contains(e.EnemyId)))
                {
                    _entityStore.Delete(enemy);
                }

                foreach (var enemy in zone.ZoneEnemies.Where(e => newIds.Contains(e.EnemyId)))
                {
                    var newData = zoneEnemiesData.ZoneEnemies.First(ze => ze.EnemyId == enemy.EnemyId);
                    _entityStore.Update(new Game.Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = enemy.ZoneId,
                        EnemyId = enemy.EnemyId,
                        Weight = newData.Weight,
                    });
                }

                var existingIds = zone.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
                var zoneEnemies = zoneEnemiesData.ZoneEnemies
                    .Where(ze => !existingIds.Contains(ze.EnemyId))
                    .Select(ze => new Game.Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = zoneEnemiesData.ZoneId,
                        EnemyId = ze.EnemyId,
                        Weight = ze.Weight,
                    }).ToList();

                _entityStore.InsertAll(zoneEnemies);
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Zone not found.");
        }
    }
}
