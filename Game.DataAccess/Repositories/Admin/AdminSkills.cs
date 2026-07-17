using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for skills and their damage portions, damage multipliers, and effects.
    /// Reuses the cached entity lookup (<see cref="ISkillEntityCache.LookupSkill"/>) for existence/diff and
    /// builds fresh, navigation-free entities for every write.
    /// </summary>
    internal class AdminSkills(ISkillEntityCache skills, IEntityStore entityStore) : IAdminSkills
    {
        private readonly ISkillEntityCache _skills = skills;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveSkills(IReadOnlyList<Change<Contracts.Skill>> changes)
        {
            // Authoring guard: a skill's rarity is a FK into the enum-seeded Rarities table, so an unmapped
            // tier (a missing/0 payload) would 500 on the FK at commit — reject it up front as a clean failure.
            if (ReferenceFieldValidation.FindUndefinedEnum(changes, s => s.RarityId, "skill rarity") is { } rejection)
            {
                return rejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(ToEntity(item)),
                edit: item =>
                {
                    var entity = ToEntity(item);
                    entity.Id = item.Id;
                    entity.RetiredAt = item.RetiredAt;
                    _entityStore.Update(entity);
                },
                key: item => item.Id,
                resourceName: "skill",
                // An edit must target an existing skill; a missing id is a not-found rejection (matching the
                // relationship setters), validated up front by the processor before anything is staged.
                editExists: item => _skills.LookupSkill(item.Id) is not null);
        }

        private static Entities.Skill ToEntity(Contracts.Skill item)
        {
            return new Entities.Skill
            {
                Name = item.Name,
                BaseDamage = item.BaseDamage,
                CriticalChance = item.CriticalChance,
                CooldownMs = item.CooldownMs,
                Description = item.Description,
                IconPath = item.IconPath,
                RarityId = (int)item.RarityId,
                Word = item.Word,
                Pronunciation = item.Pronunciation,
                Translation = item.Translation,
                Acquisition = (int)item.Acquisition,
                DesignerNotes = item.DesignerNotes,
            };
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
            return KeyedChangeSetProcessor.Apply(data.Changes, skill.SkillDamageMultipliers,
                itemKey: attribute => (int)attribute.AttributeId,
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

        public AdminSaveResult SetPortions(SetSkillPortionsData data)
        {
            var skill = _skills.LookupSkill(data.Id);
            if (skill is null)
            {
                return AdminSaveResult.NotFound("Skill");
            }

            // Anti-tamper: a non-positive weight breaks fire-time normalization (#1385) — reject it up front as
            // a clean failure rather than persisting a landmine a tampered admin client could plant.
            if (data.Changes.Any(c => c.ChangeType != EChangeType.Delete && c.Item.Weight <= 0))
            {
                return AdminSaveResult.Failure("A skill damage portion's weight must be positive.");
            }

            // Anti-tamper: a skill must keep at least one portion. Zero portions means Σweights == 0 — the same
            // fire-time-normalization landmine (#1385) — and an undefined PrimaryDamageType. The editor warns on
            // this but the warning is advisory, so the invariant is enforced here. Replay the change set's
            // membership effect (Add inserts/keeps a type, Delete removes it, Edit leaves it) against the skill's
            // current portion types; a duplicate key across change types is already rejected by the processor.
            var resultingTypes = skill.SkillDamagePortions.Select(p => p.DamageType).ToHashSet();
            foreach (var change in data.Changes)
            {
                switch (change.ChangeType)
                {
                    case EChangeType.Add:
                        resultingTypes.Add((int)change.Item.Type);
                        break;
                    case EChangeType.Delete:
                        resultingTypes.Remove((int)change.Item.Type);
                        break;
                }
            }
            if (resultingTypes.Count == 0)
            {
                return AdminSaveResult.Failure("A skill must have at least one damage portion.");
            }

            // Build a fresh, navigation-free entity per change (not the cached one, whose loaded Skill
            // back-reference would drag the whole graph into the change tracker). Portions are keyed by their
            // leaf damage type — a skill carries at most one portion per type — so the change set reconciles
            // exactly like the damage multipliers (keyed by attribute).
            return KeyedChangeSetProcessor.Apply(data.Changes, skill.SkillDamagePortions,
                itemKey: portion => (int)portion.Type,
                existingKey: p => p.DamageType,
                toEntity: portion => new Entities.SkillDamagePortion
                {
                    SkillId = skill.Id,
                    DamageType = (int)portion.Type,
                    Weight = portion.Weight,
                },
                _entityStore,
                resourceName: "skill damage portion");
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
                add: effect => _entityStore.Insert(ToEffectEntity(skill.Id, effect)),
                // Build fresh, navigation-free entities so cached back-references don't drag
                // the whole graph into the change tracker.
                edit: effect =>
                {
                    if (existingEffectIds.Contains(effect.Id))
                    {
                        var entity = ToEffectEntity(skill.Id, effect);
                        entity.Id = effect.Id;
                        _entityStore.Update(entity);
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

        private static Entities.SkillEffect ToEffectEntity(int skillId, Contracts.SkillEffect effect)
        {
            return new Entities.SkillEffect
            {
                SkillId = skillId,
                Target = (int)effect.Target,
                AttributeId = (int)effect.AttributeId,
                ModifierType = (int)effect.ModifierTypeId,
                Amount = effect.Amount,
                DurationMs = effect.DurationMs,
                ScalingAttributeId = (int)effect.ScalingAttributeId,
                ScalingAmount = effect.ScalingAmount,
            };
        }
    }
}
