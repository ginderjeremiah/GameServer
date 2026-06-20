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

        public AdminSaveResult SaveSkills(IReadOnlyList<Change<Contracts.Skill>> changes)
        {
            return ChangeSetProcessor.Apply(changes,
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
                }),
                key: item => item.Id,
                resourceName: "skill",
                // An edit must target an existing skill; a missing id is a not-found rejection (matching the
                // relationship setters), validated up front by the processor before anything is staged.
                editExists: item => _skills.LookupSkill(item.Id) is not null);
        }

        public AdminSaveResult SetMultipliers(AddEditAttributesData data)
        {
            var skill = _skills.LookupSkill(data.Id);
            if (skill is null)
            {
                return AdminSaveResult.NotFound("Skill");
            }

            // Build a fresh, navigation-free entity per change (not the cached one, whose loaded Skill
            // back-reference would drag the whole graph into the change tracker).
            return AttributeChangeSetProcessor.Apply(data.Changes, skill.SkillDamageMultipliers,
                existingKey: att => att.AttributeId,
                toEntity: attribute => new Entities.SkillDamageMultiplier
                {
                    SkillId = skill.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Multiplier = attribute.Amount,
                },
                _entityStore,
                resourceName: "skill damage multiplier");
        }

        public AdminSaveResult SetEffects(SetSkillEffectsData data)
        {
            var skill = _skills.LookupSkill(data.Id);
            if (skill is null)
            {
                return AdminSaveResult.NotFound("Skill");
            }

            // Precompute the current effect-id set once so each Edit/Delete membership guard is an O(1)
            // lookup rather than a per-change linear scan over the skill's effects (matching the other
            // change-set guards in this layer).
            var existingEffectIds = skill.SkillEffects.Select(se => se.Id).ToHashSet();

            return ChangeSetProcessor.Apply(data.Changes,
                add: effect => _entityStore.Insert(new Entities.SkillEffect
                {
                    SkillId = skill.Id,
                    Target = (int)effect.Target,
                    AttributeId = (int)effect.AttributeId,
                    ModifierType = (int)effect.ModifierTypeId,
                    Amount = effect.Amount,
                    DurationMs = effect.DurationMs,
                    ScalingAttributeId = (int)effect.ScalingAttributeId,
                    ScalingAmount = effect.ScalingAmount,
                }),
                // Build fresh, navigation-free entities so cached back-references don't drag
                // the whole graph into the change tracker.
                edit: effect =>
                {
                    if (existingEffectIds.Contains(effect.Id))
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
                            ScalingAttributeId = (int)effect.ScalingAttributeId,
                            ScalingAmount = effect.ScalingAmount,
                        });
                    }
                },
                delete: effect =>
                {
                    if (existingEffectIds.Contains(effect.Id))
                    {
                        _entityStore.Delete(new Entities.SkillEffect
                        {
                            Id = effect.Id,
                        });
                    }
                },
                key: effect => effect.Id,
                resourceName: "skill effect");
        }
    }
}
