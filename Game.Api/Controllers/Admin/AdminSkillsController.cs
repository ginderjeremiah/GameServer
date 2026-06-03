using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Game.Api.Models.Skills;
using Microsoft.AspNetCore.Mvc;
using static Game.Api.EChangeType;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting skills and their damage multipliers. The route
    /// prefix is shared across every admin controller so the existing <c>/api/AdminTools/*</c>
    /// contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminCacheInvalidationFilter))]
    public class AdminSkillsController(
        ISkills skills,
        IEntityStore entityStore) : ControllerBase
    {
        private readonly ISkills _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

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
    }
}
