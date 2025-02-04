using Game.Abstractions.DataAccess;
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
    public class AdminToolsController : ControllerBase
    {
        private readonly IRepositoryManager _repositoryManager;

        public AdminToolsController(IRepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditEnemies([FromBody] List<Change<Enemy>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Enemy
                    {
                        Name = change.Item.Name
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var enemy = _repositoryManager.Enemies.GetEnemy(change.Item.Id);
                    if (enemy is not null)
                    {
                        enemy.Name = change.Item.Name;
                        _repositoryManager.Update(enemy);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var enemy = _repositoryManager.ItemMods.GetItemMod(change.Item.Id);
                    if (enemy is not null)
                    {
                        _repositoryManager.Delete(enemy);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemAttributes([FromBody] AddEditAttributesData changeData)
        {
            var item = _repositoryManager.Items.GetItem(changeData.Id);
            if (item is null)
            {
                return ApiResponse.Error("Item does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    item.ItemAttributes.Add(new Core.Entities.ItemAttribute()
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

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemModAttributes([FromBody] AddEditAttributesData changeData)
        {
            var itemMod = _repositoryManager.ItemMods.GetItemMod(changeData.Id);
            if (itemMod is null)
            {
                return ApiResponse.Error("Item Mod does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    itemMod.ItemModAttributes.Add(new Core.Entities.ItemModAttribute()
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

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemMods([FromBody] List<Change<ItemMod>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new ItemMod
                    {
                        Name = change.Item.Name,
                        Removable = change.Item.Removable,
                        Description = change.Item.Description,
                        ItemModTypeId = change.Item.ItemModTypeId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var itemMod = _repositoryManager.ItemMods.GetItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        itemMod.Name = change.Item.Name;
                        itemMod.Removable = change.Item.Removable;
                        itemMod.Description = change.Item.Description;
                        itemMod.ItemModTypeId = change.Item.ItemModTypeId;
                        _repositoryManager.Update(itemMod);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var itemMod = _repositoryManager.ItemMods.GetItemMod(change.Item.Id);
                    if (itemMod is not null)
                    {
                        _repositoryManager.Delete(itemMod);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItems([FromBody] List<Change<Item>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Item
                    {
                        Name = change.Item.Name,
                        Description = change.Item.Description,
                        ItemCategoryId = (int)change.Item.ItemCategoryId,
                        IconPath = change.Item.IconPath,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    var item = _repositoryManager.Items.GetItem(change.Item.Id);
                    if (item is not null)
                    {
                        item.Name = change.Item.Name;
                        item.Description = change.Item.Description;
                        item.ItemCategoryId = (int)change.Item.ItemCategoryId;
                        item.IconPath = change.Item.IconPath;
                        _repositoryManager.Update(item);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var item = _repositoryManager.Items.GetItem(change.Item.Id);
                    if (item is not null)
                    {
                        _repositoryManager.Delete(item);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditItemModSlots([FromBody] List<Change<ItemModSlot>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.ItemModSlot
                    {
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                        GuaranteedItemModId = change.Item.GuaranteedItemModId,
                        Probability = change.Item.Probability,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _repositoryManager.Update(new Core.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                        ItemId = change.Item.ItemId,
                        ItemModSlotTypeId = (int)change.Item.ItemModSlotTypeId,
                        GuaranteedItemModId = change.Item.GuaranteedItemModId,
                        Probability = change.Item.Probability,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _repositoryManager.Delete(new Core.Entities.ItemModSlot
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditSkills([FromBody] List<Change<Skill>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Skill
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
                    _repositoryManager.Update(new Core.Entities.Skill
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
                    _repositoryManager.Delete(new Core.Entities.Skill
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditTags([FromBody] List<Change<Tag>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Tag
                    {
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Edit)
                {
                    _repositoryManager.Update(new Core.Entities.Tag
                    {
                        Id = change.Item.Id,
                        Name = change.Item.Name,
                        TagCategoryId = change.Item.TagCategoryId,
                    });
                }
                else if (change.ChangeType == Delete)
                {
                    _repositoryManager.Delete(new Core.Entities.Tag
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> AddEditZones([FromBody] List<Change<Zone>> changes)
        {
            foreach (var change in changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.Zone
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
                    _repositoryManager.Update(new Core.Entities.Zone
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
                    _repositoryManager.Delete(new Core.Entities.Zone
                    {
                        Id = change.Item.Id,
                    });
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetEnemyAttributeDistributions([FromBody] SetEnemyAttributeDistributions distributionsData)
        {
            var enemy = _repositoryManager.Enemies.GetEnemy(distributionsData.EnemyId);
            if (enemy is not null)
            {
                var newIds = distributionsData.AttributeDistributions.Select(ad => (int)ad.AttributeId).ToList();
                foreach (var dist in enemy.AttributeDistributions.Where(ad => !newIds.Contains(ad.AttributeId)))
                {
                    _repositoryManager.Delete(enemy);
                }

                foreach (var dist in enemy.AttributeDistributions.Where(ad => newIds.Contains(ad.AttributeId)))
                {
                    var newData = distributionsData.AttributeDistributions.First(ad => (int)ad.AttributeId == dist.AttributeId);
                    _repositoryManager.Update(new Core.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = dist.AttributeId,
                        BaseAmount = newData.AmountPerLevel,
                        AmountPerLevel = newData.AmountPerLevel
                    });
                }

                var existingIds = enemy.AttributeDistributions.Select(ad => ad.AttributeId).ToList();
                var zoneEnemies = distributionsData.AttributeDistributions
                    .Where(ad => !existingIds.Contains((int)ad.AttributeId))
                    .Select(ad => new Core.Entities.AttributeDistribution
                    {
                        EnemyId = enemy.Id,
                        AttributeId = (int)ad.AttributeId,
                        BaseAmount = ad.AmountPerLevel,
                        AmountPerLevel = ad.AmountPerLevel
                    }).ToList();

                _repositoryManager.InsertAll(zoneEnemies);
                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Zone not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetEnemySkills([FromBody] SetEnemySkillsData enemySkillsData)
        {
            var enemy = _repositoryManager.Enemies.GetEnemy(enemySkillsData.EnemyId);
            if (enemy is not null)
            {
                var newIds = enemySkillsData.SkillIds;
                foreach (var skill in enemy.EnemySkills.Where(e => !newIds.Contains(e.SkillId)))
                {
                    _repositoryManager.Delete(skill);
                }

                var existingIds = enemy.EnemySkills.Select(ze => ze.SkillId).ToList();
                var enemySkills = enemySkillsData.SkillIds
                    .Where(id => !existingIds.Contains(id))
                    .Select(id => new Core.Entities.EnemySkill
                    {
                        EnemyId = enemy.Id,
                        SkillId = id,
                    }).ToList();

                _repositoryManager.InsertAll(enemySkills);
                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Zone not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetSkillMultipliers([FromBody] AddEditAttributesData changeData)
        {
            var skill = _repositoryManager.Skills.GetSkill(changeData.Id);
            if (skill is null)
            {
                return ApiResponse.Error("Skill does not exist.");
            }

            foreach (var change in changeData.Changes.OrderByDescending(c => c.ChangeType))
            {
                if (change.ChangeType == Add)
                {
                    _repositoryManager.Insert(new Core.Entities.SkillDamageMultiplier
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
                        _repositoryManager.Update(att);
                    }
                }
                else if (change.ChangeType == Delete)
                {
                    var att = skill.SkillDamageMultipliers.FirstOrDefault(att => (int)change.Item.AttributeId == att.AttributeId);
                    if (att is not null)
                    {
                        skill.SkillDamageMultipliers.Remove(att);
                        _repositoryManager.Delete(att);
                    }
                }
            }

            await _repositoryManager.SaveChangesAsync();
            return ApiResponse.Success();
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItem([FromBody] SetTagsData setTagsData)
        {
            var item = _repositoryManager.Items.GetItem(setTagsData.Id);
            if (item is not null)
            {
                item.Tags.Clear();
                var tags = _repositoryManager.Tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    item.Tags.Add(tag);
                }

                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetTagsForItemMod([FromBody] SetTagsData setTagsData)
        {
            var itemMod = _repositoryManager.ItemMods.GetItemMod(setTagsData.Id);
            if (itemMod is not null)
            {
                itemMod.Tags.Clear();
                var tags = _repositoryManager.Tags.GetTags(setTagsData.TagIds);
                await foreach (var tag in tags)
                {
                    itemMod.Tags.Add(tag);
                }

                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Item mod not found.");
        }

        [HttpPost]
        public async Task<ApiResponse> SetZoneEnemies([FromBody] SetZoneEnemiesData zoneEnemiesData)
        {
            var zone = _repositoryManager.Zones.GetZone(zoneEnemiesData.ZoneId);
            if (zone is not null)
            {
                var newIds = zoneEnemiesData.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
                foreach (var enemy in zone.ZoneEnemies.Where(e => !newIds.Contains(e.EnemyId)))
                {
                    _repositoryManager.Delete(enemy);
                }

                foreach (var enemy in zone.ZoneEnemies.Where(e => newIds.Contains(e.EnemyId)))
                {
                    var newData = zoneEnemiesData.ZoneEnemies.First(ze => ze.EnemyId == enemy.EnemyId);
                    _repositoryManager.Update(new Core.Entities.ZoneEnemy
                    {
                        ZoneId = enemy.ZoneId,
                        EnemyId = enemy.EnemyId,
                        Weight = newData.Weight,
                    });
                }

                var existingIds = zone.ZoneEnemies.Select(ze => ze.EnemyId).ToList();
                var zoneEnemies = zoneEnemiesData.ZoneEnemies
                    .Where(ze => !existingIds.Contains(ze.EnemyId))
                    .Select(ze => new Core.Entities.ZoneEnemy
                    {
                        ZoneId = zoneEnemiesData.ZoneId,
                        EnemyId = ze.EnemyId,
                        Weight = ze.Weight,
                    }).ToList();

                _repositoryManager.InsertAll(zoneEnemies);
                await _repositoryManager.SaveChangesAsync();
                return ApiResponse.Success();
            }

            return ApiResponse.Error("Zone not found.");
        }
    }
}
