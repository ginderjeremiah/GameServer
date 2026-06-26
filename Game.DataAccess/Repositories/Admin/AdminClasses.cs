using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Core.Players.Inventories;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for classes and their related collections. Reuses the cached entity
    /// lookups for existence/diff and builds fresh, navigation-free entities for every write. The identity
    /// save is retire-only (no hard delete) and carries the signature-passive scalar fields; the relationship
    /// setters reconcile a full desired set.
    /// </summary>
    internal class AdminClasses(
        IClassEntityCache classes,
        ISkillEntityCache skills,
        IItemEntityCache items,
        IEntityStore entityStore) : IAdminClasses
    {
        private readonly IClassEntityCache _classes = classes;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IItemEntityCache _items = items;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveClasses(IReadOnlyList<Change<Contracts.Class>> changes)
        {
            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Class
                {
                    Name = item.Name,
                    Description = item.Description,
                    Word = item.Word,
                    PassiveAttributeId = (int)item.PassiveAttributeId,
                    PassiveAmount = item.PassiveAmount,
                    PassiveScalingAttributeId = (int?)item.PassiveScalingAttributeId,
                    PassiveScalingAmount = item.PassiveScalingAmount,
                    PassiveModifierType = (int)item.PassiveModifierType,
                }),
                edit: item => _entityStore.Update(new Entities.Class
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    Word = item.Word,
                    PassiveAttributeId = (int)item.PassiveAttributeId,
                    PassiveAmount = item.PassiveAmount,
                    PassiveScalingAttributeId = (int?)item.PassiveScalingAttributeId,
                    PassiveScalingAmount = item.PassiveScalingAmount,
                    PassiveModifierType = (int)item.PassiveModifierType,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "class",
                editExists: item => _classes.LookupClass(item.Id) is not null);
        }

        public AdminSaveResult SetStarterSkills(SetClassStarterSkillsData data)
        {
            var @class = _classes.LookupClass(data.ClassId);
            if (@class is null)
            {
                return AdminSaveResult.NotFound("Class");
            }

            // Anti-tamper: a starter skill is a permanent player grant, so every assigned skill must declare
            // itself Player-acquirable (the flag is intent; this reference is reality). The whole desired set
            // is checked, so a skill whose flag was later cleared can no longer be re-saved onto the class.
            if (FindStarterSkillFlagViolation(data.SkillIds) is { } rejection)
            {
                return rejection;
            }

            // A ClassStarterSkill is a pure join row, so a skill present on both sides needs no update.
            return ChildCollectionReconciler.Reconcile(
                existing: @class.StarterSkills,
                desired: data.SkillIds,
                existingKey: s => s.SkillId,
                desiredKey: id => id,
                delete: s => _entityStore.Delete(new Entities.ClassStarterSkill
                {
                    ClassId = @class.Id,
                    SkillId = s.SkillId,
                }),
                insert: id => _entityStore.Insert(new Entities.ClassStarterSkill
                {
                    ClassId = @class.Id,
                    SkillId = id,
                }),
                resourceName: "starter skill");
        }

        public AdminSaveResult SetStarterEquipment(SetClassStarterEquipmentData data)
        {
            var @class = _classes.LookupClass(data.ClassId);
            if (@class is null)
            {
                return AdminSaveResult.NotFound("Class");
            }

            // Anti-tamper: each starter item must exist and its category must match the slot it is equipped
            // into (the runtime equip path enforces this too; a tampered admin client can't bypass it).
            if (FindStarterEquipmentViolation(data.Equipment) is { } rejection)
            {
                return rejection;
            }

            return ChildCollectionReconciler.Reconcile(
                existing: @class.StarterEquipment,
                desired: data.Equipment,
                existingKey: e => e.EquipmentSlotId,
                desiredKey: e => (int)e.EquipmentSlot,
                delete: e => _entityStore.Delete(new Entities.ClassStarterEquipment
                {
                    ClassId = @class.Id,
                    EquipmentSlotId = e.EquipmentSlotId,
                }),
                insert: e => _entityStore.Insert(ToEquipmentEntity(@class.Id, e)),
                resourceName: "starter equipment",
                update: e => _entityStore.Update(ToEquipmentEntity(@class.Id, e)));
        }

        public AdminSaveResult SetAttributeDistributions(SetClassAttributeDistributionsData data)
        {
            var @class = _classes.LookupClass(data.ClassId);
            if (@class is null)
            {
                return AdminSaveResult.NotFound("Class");
            }

            // Authoring guard: these distributions become the class's level-scaled locked base, folded into the
            // battler's AttributeCollection at assembly (BattleSnapshot.GetModifiers), which sums per-attribute
            // modifiers. A duplicate attribute would silently double-count its locked-base modifier, and a
            // non-core attribute's modifier would silently take effect (while being omitted from the
            // reward-power heuristic, DefeatRewards.SumCoreAttributes) — neither is intended, so both are
            // rejected here at save time.
            if (FindAttributeDistributionViolation(data.AttributeDistributions) is { } rejection)
            {
                return rejection;
            }

            return ChildCollectionReconciler.Reconcile(
                existing: @class.AttributeDistributions,
                desired: data.AttributeDistributions,
                existingKey: ad => ad.AttributeId,
                desiredKey: ad => (int)ad.AttributeId,
                delete: ad => _entityStore.Delete(new Entities.ClassAttributeDistribution
                {
                    ClassId = @class.Id,
                    AttributeId = ad.AttributeId,
                }),
                insert: ad => _entityStore.Insert(ToDistributionEntity(@class.Id, ad)),
                resourceName: "attribute distribution",
                update: ad => _entityStore.Update(ToDistributionEntity(@class.Id, ad)));
        }

        private static Entities.ClassStarterEquipment ToEquipmentEntity(int classId, Contracts.ClassStarterEquipment equipment)
        {
            return new Entities.ClassStarterEquipment
            {
                ClassId = classId,
                EquipmentSlotId = (int)equipment.EquipmentSlot,
                ItemId = equipment.ItemId,
            };
        }

        private static Entities.ClassAttributeDistribution ToDistributionEntity(int classId, Contracts.AttributeDistribution distribution)
        {
            return new Entities.ClassAttributeDistribution
            {
                ClassId = classId,
                AttributeId = (int)distribution.AttributeId,
                BaseAmount = distribution.BaseAmount,
                AmountPerLevel = distribution.AmountPerLevel,
            };
        }

        /// <summary>Returns a rejection for the first desired distribution that targets a non-core attribute or
        /// repeats an attribute already in the set, or null when the desired set is well-formed.</summary>
        private static AdminSaveResult? FindAttributeDistributionViolation(IEnumerable<Contracts.AttributeDistribution> distributions)
        {
            var seen = new HashSet<EAttribute>();
            foreach (var distribution in distributions)
            {
                if (!Game.Core.Attributes.Attribute.IsCore(distribution.AttributeId))
                {
                    return AdminSaveResult.Failure(
                        $"Attribute '{distribution.AttributeId}' is not a core attribute and cannot have a class distribution.");
                }

                if (!seen.Add(distribution.AttributeId))
                {
                    return AdminSaveResult.Failure(
                        $"Attribute '{distribution.AttributeId}' has more than one distribution; each attribute may appear at most once.");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection for the first desired skill that does not exist or is not
        /// <see cref="ESkillAcquisition.Player"/>-flagged, or null when every assigned skill is valid.</summary>
        private AdminSaveResult? FindStarterSkillFlagViolation(IEnumerable<int> skillIds)
        {
            foreach (var skillId in skillIds)
            {
                var skill = _skills.LookupSkill(skillId);
                if (skill is null)
                {
                    return AdminSaveResult.Failure($"Skill {skillId} does not exist.");
                }

                if (!((ESkillAcquisition)skill.Acquisition).HasFlag(ESkillAcquisition.Player))
                {
                    return AdminSaveResult.Failure(
                        $"Skill '{skill.Name}' is not flagged as Player-acquirable and cannot be a class starter skill.");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection for the first desired equipment whose item does not exist or whose
        /// category does not match the slot it is equipped into, or null when every entry is valid.</summary>
        private AdminSaveResult? FindStarterEquipmentViolation(IEnumerable<Contracts.ClassStarterEquipment> equipment)
        {
            foreach (var entry in equipment)
            {
                var item = _items.LookupItem(entry.ItemId);
                if (item is null)
                {
                    return AdminSaveResult.Failure($"Item {entry.ItemId} does not exist.");
                }

                var requiredCategory = new EquipmentSlot(entry.EquipmentSlot).ItemCategory;
                if (item.ItemCategoryId != (int)requiredCategory)
                {
                    return AdminSaveResult.Failure(
                        $"Item '{item.Name}' cannot be equipped into the {entry.EquipmentSlot} slot (requires a {requiredCategory} item).");
                }
            }

            return null;
        }
    }
}
