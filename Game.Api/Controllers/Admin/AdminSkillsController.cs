using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Filters;
using Game.Api.Models.Common;
using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Controllers.Admin
{
    /// <summary>
    /// Admin Workbench endpoints for persisting skills and their damage multipliers. The route
    /// prefix is shared across every admin controller so the existing <c>/api/AdminTools/*</c>
    /// contract is preserved.
    /// </summary>
    [Route("/api/AdminTools/[action]")]
    [ApiController]
    [ServiceFilter(typeof(AdminRoleAuthorizationFilter))]
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
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Abstractions.Entities.Skill
                {
                    Name = item.Name,
                    BaseDamage = item.BaseDamage,
                    CooldownMs = item.CooldownMs,
                    Description = item.Description,
                    IconPath = item.IconPath,
                }),
                edit: item => _entityStore.Update(new Abstractions.Entities.Skill
                {
                    Id = item.Id,
                    Name = item.Name,
                    BaseDamage = item.BaseDamage,
                    CooldownMs = item.CooldownMs,
                    Description = item.Description,
                    IconPath = item.IconPath,
                }),
                delete: item => _entityStore.Delete(new Abstractions.Entities.Skill
                {
                    Id = item.Id,
                    Name = "",
                    Description = "",
                    IconPath = "",
                }));

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

            ChangeSetProcessor.Apply(changeData.Changes,
                add: attribute => _entityStore.Insert(new Abstractions.Entities.SkillDamageMultiplier
                {
                    SkillId = skill.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Multiplier = attribute.Amount,
                }),
                // Operate on a fresh, navigation-free entity (not the cached one, whose loaded
                // Skill back-reference would drag the whole graph into the change tracker).
                edit: attribute =>
                {
                    if (skill.SkillDamageMultipliers.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Update(new Abstractions.Entities.SkillDamageMultiplier
                        {
                            SkillId = skill.Id,
                            AttributeId = (int)attribute.AttributeId,
                            Multiplier = attribute.Amount,
                        });
                    }
                },
                delete: attribute =>
                {
                    if (skill.SkillDamageMultipliers.Any(att => (int)attribute.AttributeId == att.AttributeId))
                    {
                        _entityStore.Delete(new Abstractions.Entities.SkillDamageMultiplier
                        {
                            SkillId = skill.Id,
                            AttributeId = (int)attribute.AttributeId,
                        });
                    }
                });

            return ApiResponse.Success();
        }
    }
}
