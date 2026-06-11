using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for skills and their damage multipliers. Reuses the cached entity
    /// lookup (<see cref="ISkillEntityCache.LookupSkill"/>) for existence/diff and builds fresh,
    /// navigation-free entities for every write.
    /// </summary>
    internal class AdminSkills(ISkillEntityCache skills, IEntityStore entityStore) : IAdminSkills
    {
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public void SaveSkills(IReadOnlyList<Change<Contracts.Skill>> changes)
        {
            ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Skill
                {
                    Name = item.Name,
                    BaseDamage = item.BaseDamage,
                    CooldownMs = item.CooldownMs,
                    Description = item.Description,
                    IconPath = item.IconPath,
                }),
                edit: item => _entityStore.Update(new Entities.Skill
                {
                    Id = item.Id,
                    Name = item.Name,
                    BaseDamage = item.BaseDamage,
                    CooldownMs = item.CooldownMs,
                    Description = item.Description,
                    IconPath = item.IconPath,
                    RetiredAt = item.RetiredAt,
                }));
        }

        public bool SetMultipliers(AddEditAttributesData data)
        {
            var skill = _skills.LookupSkill(data.Id);
            if (skill is null)
            {
                return false;
            }

            ChangeSetProcessor.Apply(data.Changes,
                add: attribute => _entityStore.Insert(new Entities.SkillDamageMultiplier
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
                        _entityStore.Update(new Entities.SkillDamageMultiplier
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
                        _entityStore.Delete(new Entities.SkillDamageMultiplier
                        {
                            SkillId = skill.Id,
                            AttributeId = (int)attribute.AttributeId,
                        });
                    }
                });

            return true;
        }

        public bool SetEffects(SetSkillEffectsData data)
        {
            var skill = _skills.LookupSkill(data.Id);
            if (skill is null)
            {
                return false;
            }

            ChangeSetProcessor.Apply(data.Changes,
                add: effect => _entityStore.Insert(new Entities.SkillEffect
                {
                    SkillId = skill.Id,
                    Target = (int)effect.Target,
                    AttributeId = (int)effect.AttributeId,
                    ModifierType = (int)effect.ModifierTypeId,
                    Amount = effect.Amount,
                    DurationMs = effect.DurationMs,
                }),
                // Build fresh, navigation-free entities so cached back-references don't drag
                // the whole graph into the change tracker.
                edit: effect =>
                {
                    if (skill.SkillEffects.Any(se => se.Id == effect.Id))
                    {
                        _entityStore.Update(new Entities.SkillEffect
                        {
                            Id = effect.Id,
                            SkillId = skill.Id,
                            Target = (int)effect.Target,
                            AttributeId = (int)effect.AttributeId,
                            ModifierType = (int)effect.ModifierTypeId,
                            Amount = effect.Amount,
                            DurationMs = effect.DurationMs,
                        });
                    }
                },
                delete: effect =>
                {
                    if (skill.SkillEffects.Any(se => se.Id == effect.Id))
                    {
                        _entityStore.Delete(new Entities.SkillEffect
                        {
                            Id = effect.Id,
                        });
                    }
                });

            return true;
        }
    }
}
