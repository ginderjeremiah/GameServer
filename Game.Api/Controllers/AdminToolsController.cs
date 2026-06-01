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
                    _entityStore.Insert(new Abstractions.Entities.Enemy
                    {
                        Name = change.Item.Name,
                        IsBoss = change.Item.IsBoss,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.Enemy
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        IsBoss = change.Item.IsBoss,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.Enemy
                    {
                        Id = change.Item.Id,
                        Name = "",
                    });
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
                    _entityStore.Insert(new Abstractions.Entities.ItemAttribute
                    {
                        ItemId = item.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                    // Item back-reference would drag the whole graph into the change tracker).
                    if (item.ItemAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)change.Item.AttributeId,
                            Amount = change.Item.Amount,
                        });
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    if (item.ItemAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.ItemAttribute
                        {
                            ItemId = item.Id,
                            AttributeId = (int)change.Item.AttributeId,
                        });
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
                    _entityStore.Insert(new Abstractions.Entities.ItemModAttribute
                    {
                        ItemModId = itemMod.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Amount = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                    // ItemMod back-reference would drag the whole graph into the change tracker).
                    if (itemMod.ItemModAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)change.Item.AttributeId,
                            Amount = change.Item.Amount,
                        });
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    if (itemMod.ItemModAttributes.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.ItemModAttribute
                        {
                            ItemModId = itemMod.Id,
                            AttributeId = (int)change.Item.AttributeId,
                        });
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
                    _entityStore.Insert(new Abstractions.Entities.ItemMod
                    {
                        Name = change.Item.Name,
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
                    _entityStore.Insert(new Abstractions.Entities.Item
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemCategoryId = (int)change.Item.ItemCategoryId,
                        RarityId = (int)change.Item.RarityId,
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
                        item.RarityId = (int)change.Item.RarityId;
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
                    _entityStore.Insert(new Abstractions.Entities.ItemModSlot
                    {
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.ItemModSlot
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
                    _entityStore.Insert(new Abstractions.Entities.Skill
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
                    _entityStore.Update(new Abstractions.Entities.Skill
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
                    _entityStore.Delete(new Abstractions.Entities.Skill
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
                    _entityStore.Insert(new Abstractions.Entities.Tag
                    {
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _entityStore.Update(new Abstractions.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _entityStore.Delete(new Abstractions.Entities.Tag
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
                    _entityStore.Insert(new Abstractions.Entities.Zone
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
                    _entityStore.Update(new Abstractions.Entities.Zone
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
                    _entityStore.Delete(new Abstractions.Entities.Zone
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
                    _entityStore.Update(new Abstractions.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = dist.AttributeId,
                        BaseAmount = newData.BaseAmount,
                        AmountPerLevel = newData.AmountPerLevel
                    });
                }

                var existingIds = enemy.AttributeDistributions.Select(ad => ad.AttributeId).ToList();
                var newDistributions = distributionsData.AttributeDistributions
                    .Where(ad => !existingIds.Contains((int)ad.AttributeId))
                    .Select(ad => new Abstractions.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = (int)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
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
                    .Select(id => new Abstractions.Entities.EnemySkill
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
        public ApiResponse SetEnemySpawns([FromBody] SetEnemySpawnsData spawnsData)
        {
            var enemy = _enemies.GetEnemy(spawnsData.EnemyId);
            if (enemy is not null)
            {
                var newZoneIds = spawnsData.Spawns.Select(s => s.ZoneId).ToList();
                foreach (var spawn in enemy.ZoneEnemies.Where(ze => !newZoneIds.Contains(ze.ZoneId)))
                {
                    _entityStore.Delete(spawn);
                }

                foreach (var spawn in enemy.ZoneEnemies.Where(ze => newZoneIds.Contains(ze.ZoneId)))
                {
                    var newData = spawnsData.Spawns.First(s => s.ZoneId == spawn.ZoneId);
                    _entityStore.Update(new Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = spawn.ZoneId,
                        EnemyId = enemy.Id,
                        Weight = newData.Weight,
                    });
                }

                var existingZoneIds = enemy.ZoneEnemies.Select(ze => ze.ZoneId).ToList();
                var newSpawns = spawnsData.Spawns
                    .Where(s => !existingZoneIds.Contains(s.ZoneId))
                    .Select(s => new Abstractions.Entities.ZoneEnemy
                    {
                        ZoneId = s.ZoneId,
                        EnemyId = enemy.Id,
                        Weight = s.Weight,
                    }).ToList();

                _entityStore.InsertAll(newSpawns);
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
                    _entityStore.Insert(new Abstractions.Entities.SkillDamageMultiplier
                    {
                        SkillId = skill.Id,
                        AttributeId = (int)change.Item.AttributeId,
                        Multiplier = change.Item.Amount,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                    // Skill back-reference would drag the whole graph into the change tracker).
                    if (skill.SkillDamageMultipliers.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.SkillDamageMultiplier
                        {
                            SkillId = skill.Id,
                            AttributeId = (int)change.Item.AttributeId,
                            Multiplier = change.Item.Amount,
                        });
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    if (skill.SkillDamageMultipliers.Any(att => (int)change.Item.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.SkillDamageMultiplier
                        {
                            SkillId = skill.Id,
                            AttributeId = (int)change.Item.AttributeId,
                        });
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
                _entityStore.Track(item);
                var currentTags = await _tags.GetTagsForItem(setTagsData.Id).ToListAsync();
                foreach (var currentTag in currentTags)
                {
                    if (!setTagsData.TagIds.Contains(currentTag.Id))
                    {
                        currentTag.Items.Clear();
                    }
                }

                var tags = _tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    if (!currentTags.Any(t => t.Id == tag.Id))
                    {
                        tag.Items = [];
                        tag.Items.Add(item);
                    }
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
                _entityStore.Track(itemMod);
                var currentTags = await _tags.GetTagsForItemMod(setTagsData.Id).ToListAsync();
                foreach (var currentTag in currentTags)
                {
                    if (!setTagsData.TagIds.Contains(currentTag.Id))
                    {
                        currentTag.ItemMods.Clear();
                    }
                }

                var tags = _tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    if (!currentTags.Any(t => t.Id == tag.Id))
                    {
                        tag.ItemMods = [];
                        tag.ItemMods.Add(itemMod);
                    }
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
