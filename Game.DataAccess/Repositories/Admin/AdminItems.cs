using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.Core.Attributes;
using Game.Core.Skills;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for items and their related collections. Reuses the cached entity
    /// lookup (<see cref="IItemEntityCache.LookupItem"/>) for existence/diff and the tag-assignment queries
    /// on <see cref="ITagAssignmentQueries"/>; all writes go through the entity store. Changes are staged on
    /// the unit of work; the per-action commit filter persists them.
    /// </summary>
    internal class AdminItems(
        IItemEntityCache items, ISkillEntityCache skills, IProficiencyEntityCache proficiencies,
        ITagAssignmentQueries tags, IAppliedModQueries appliedMods, IEntityStore entityStore)
        : IAdminItems
    {
        private readonly IItemEntityCache _items = items;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IProficiencyEntityCache _proficiencies = proficiencies;
        private readonly ITagAssignmentQueries _tags = tags;
        private readonly IAppliedModQueries _appliedMods = appliedMods;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveItems(IReadOnlyList<Change<Contracts.Item>> changes)
        {
            // Authoring guards (rejected up front before anything is staged). The rarity/category FKs point at
            // enum-seeded reference tables, so an unmapped value would 500 on the FK at commit — reject it cleanly.
            if (ReferenceFieldValidation.FindUndefinedEnum(changes, i => i.RarityId, "item rarity") is { } rarityRejection)
            {
                return rarityRejection;
            }

            if (ReferenceFieldValidation.FindUndefinedEnum(changes, i => i.ItemCategoryId, "item category") is { } categoryRejection)
            {
                return categoryRejection;
            }

            // The no-stranding invariant: a weapon must declare both a WeaponType (any damage-type leaf) and a
            // signature GrantedSkill that is itself fieldable with that weapon (a martial signature must match
            // its own weapon's type; a non-martial one always qualifies), guaranteeing the player always fields
            // ≥1 usable skill even when every selected skill is dimmed by the weapon-match gate. A non-weapon
            // item may not carry a WeaponType. Enforced server-side as anti-tamper (a tampered client can't
            // bypass the editor).
            if (FindWeaponInvariantViolation(changes) is { } weaponRejection)
            {
                return weaponRejection;
            }

            // Anti-tamper: a skill an item grants must declare itself Item-acquirable. The flag is the declared
            // intent; this reference is the reality, so the save bridges them (a tampered admin client can't
            // bypass the filtered picker).
            if (FindGrantedSkillFlagViolation(changes) is { } rejection)
            {
                return rejection;
            }

            // A proficiency gate must reference an existing, non-retired proficiency and name a level within
            // that proficiency's range — a bad FK would otherwise FK-fault at commit, and an out-of-range
            // level would author an unsatisfiable (or no-op) gate.
            if (FindProficiencyGateViolation(changes) is { } gateRejection)
            {
                return gateRejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Item
                {
                    Name = item.Name,
                    Description = item.Description,
                    ItemCategoryId = (int)item.ItemCategoryId,
                    RarityId = (int)item.RarityId,
                    IconPath = item.IconPath,
                    GrantedSkillId = item.GrantedSkillId,
                    WeaponType = (int?)item.WeaponType,
                    RequiredProficiencyId = item.RequiredProficiencyId,
                    RequiredProficiencyLevel = item.RequiredProficiencyId is null ? 0 : item.RequiredProficiencyLevel,
                    DesignerNotes = item.DesignerNotes,
                }),
                // Build a fresh, navigation-free entity rather than mutating the cached one, whose loaded
                // graph would otherwise be dragged into the change tracker.
                edit: item => _entityStore.Update(new Entities.Item
                {
                    Id = item.Id,
                    Name = item.Name,
                    Description = item.Description,
                    ItemCategoryId = (int)item.ItemCategoryId,
                    RarityId = (int)item.RarityId,
                    IconPath = item.IconPath,
                    GrantedSkillId = item.GrantedSkillId,
                    WeaponType = (int?)item.WeaponType,
                    RequiredProficiencyId = item.RequiredProficiencyId,
                    RequiredProficiencyLevel = item.RequiredProficiencyId is null ? 0 : item.RequiredProficiencyLevel,
                    DesignerNotes = item.DesignerNotes,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "item",
                // An edit must target an existing item; a missing id is a not-found rejection (matching the
                // relationship setters), validated up front by the processor before anything is staged.
                editExists: item => _items.LookupItem(item.Id) is not null);
        }

        /// <summary>
        /// Returns a rejection for the first added/edited item that breaks the weapon no-stranding invariant,
        /// or null when every item is valid. A <see cref="EItemCategory.Weapon"/> item must declare a
        /// <c>WeaponType</c> — any damage-type leaf, so a caster weapon can declare its element (e.g. a Fire
        /// staff) rather than a martial leaf — and a <c>GrantedSkillId</c>. The granted signature must itself be
        /// fieldable while <em>this</em> weapon is equipped: a martial (weapon-leaf) signature must match the
        /// weapon's own type, while a non-martial signature (elemental/DoT/caster) is never gated and always
        /// qualifies. Either way the signature is what keeps the player with ≥1 usable skill once the
        /// weapon-match gate dims the rest. A non-weapon item may not carry a <c>WeaponType</c> (it is only
        /// meaningful on a weapon). Deletes are skipped.
        /// </summary>
        private AdminSaveResult? FindWeaponInvariantViolation(IReadOnlyList<Change<Contracts.Item>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var item = change.Item;
                if (item.ItemCategoryId == EItemCategory.Weapon)
                {
                    if (item.WeaponType is not { } weaponType)
                    {
                        return AdminSaveResult.Failure("A weapon item must declare a weapon type.");
                    }

                    if (!Enum.IsDefined(weaponType))
                    {
                        return AdminSaveResult.Failure($"'{weaponType}' is not a valid weapon type.");
                    }

                    if (item.GrantedSkillId is not { } grantedSkillId)
                    {
                        return AdminSaveResult.Failure("A weapon item must grant a signature skill.");
                    }

                    // A missing/unresolvable skill is reported by FindGrantedSkillFlagViolation, which runs
                    // next; skip the signature-type check here rather than duplicating that rejection.
                    if (_skills.LookupSkill(grantedSkillId) is { } grantedSkill)
                    {
                        var signatureType = PrimaryDamageTypeResolver.Resolve(
                            grantedSkill.SkillDamagePortions, p => p.Weight, p => (EDamageType)p.DamageType);
                        if (DamageTypes.IsWeaponLeaf(signatureType) && signatureType != weaponType)
                        {
                            return AdminSaveResult.Failure(
                                $"'{weaponType}' weapon grants a '{signatureType}' signature skill, which strands the wielder — a martial signature must match its own weapon's type.");
                        }
                    }
                }
                else if (item.WeaponType is not null)
                {
                    return AdminSaveResult.Failure("Only a weapon item can declare a weapon type.");
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a rejection for the first added/edited item whose <c>GrantedSkillId</c> targets a skill
        /// that is not <see cref="ESkillAcquisition.Item"/>-flagged (or does not exist), or null when every
        /// granted skill is valid. Deletes carry no grant and are skipped.
        /// </summary>
        private AdminSaveResult? FindGrantedSkillFlagViolation(IReadOnlyList<Change<Contracts.Item>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete || change.Item.GrantedSkillId is not { } skillId)
                {
                    continue;
                }

                var skill = _skills.LookupSkill(skillId);
                if (skill is null)
                {
                    return AdminSaveResult.Failure($"Granted skill {skillId} does not exist.");
                }

                if (!((ESkillAcquisition)skill.Acquisition).HasFlag(ESkillAcquisition.Item))
                {
                    return AdminSaveResult.Failure(
                        $"Skill '{skill.Name}' is not flagged as Item-acquirable and cannot be granted by an item.");
                }
            }

            return null;
        }

        /// <summary>
        /// Returns a rejection for the first added/edited item whose <c>RequiredProficiencyId</c> targets a
        /// proficiency that does not exist or is retired, or whose <c>RequiredProficiencyLevel</c> is outside
        /// the proficiency's <c>[1, MaxLevel]</c> range, or null when every gate is valid. Ungated items and
        /// deletes are skipped.
        /// </summary>
        private AdminSaveResult? FindProficiencyGateViolation(IReadOnlyList<Change<Contracts.Item>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete || change.Item.RequiredProficiencyId is not { } proficiencyId)
                {
                    continue;
                }

                var proficiency = _proficiencies.LookupProficiency(proficiencyId);
                if (proficiency is null)
                {
                    return AdminSaveResult.Failure($"Required proficiency {proficiencyId} does not exist.");
                }

                if (proficiency.RetiredAt is not null)
                {
                    return AdminSaveResult.Failure(
                        $"Proficiency '{proficiency.Name}' is retired and cannot gate an item.");
                }

                var level = change.Item.RequiredProficiencyLevel;
                if (level < 1 || level > proficiency.MaxLevel)
                {
                    return AdminSaveResult.Failure(
                        $"Required proficiency level {level} is outside the valid range for '{proficiency.Name}' (1 to {proficiency.MaxLevel}).");
                }
            }

            return null;
        }

        public AdminSaveResult SetAttributes(AddEditAttributesData data)
        {
            var item = _items.LookupItem(data.Id);
            if (item is null)
            {
                return AdminSaveResult.NotFound("Item");
            }

            // Build a fresh, navigation-free entity per change (not the cached one, whose loaded Item
            // back-reference would drag the whole graph into the change tracker).
            return KeyedChangeSetProcessor.Apply(data.Changes, item.ItemAttributes,
                itemKey: attribute => (int)attribute.AttributeId,
                existingKey: att => att.AttributeId,
                toEntity: attribute => new Entities.ItemAttribute
                {
                    ItemId = item.Id,
                    AttributeId = (int)attribute.AttributeId,
                    Amount = attribute.Amount,
                },
                _entityStore,
                resourceName: "item attribute");
        }

        public async Task<AdminSaveResult> SaveModSlots(
            IReadOnlyList<Change<Contracts.ItemModSlot>> changes, CancellationToken cancellationToken = default)
        {
            // An Add must target an existing owning item — a bad ItemId would otherwise FK-fault at commit
            // as an opaque 500. Reject the whole batch up front so the commit filter doesn't persist the
            // rest alongside the invalid add (matching the identity-level saves' up-front validation).
            if (changes.Any(c => c.ChangeType == EChangeType.Add && _items.LookupItem(c.Item.ItemId) is null))
            {
                return AdminSaveResult.NotFound("Item");
            }

            // A deleted slot that is still occupied by a player's applied mod would otherwise FK-fault at
            // commit (or, pre-#1885, cascade-delete every occupying player's AppliedMod row outright). Reject
            // up front as a graceful failure, mirroring the other content-lifecycle retirement guards.
            var deletedSlotIds = changes
                .Where(c => c.ChangeType == EChangeType.Delete)
                .Select(c => c.Item.Id)
                .ToList();
            if (deletedSlotIds.Count > 0)
            {
                var occupiedSlotIds = await _appliedMods.GetOccupiedSlotIds(deletedSlotIds).ToHashSetAsync(cancellationToken: cancellationToken);
                if (occupiedSlotIds.Count > 0)
                {
                    return AdminSaveResult.Failure(
                        $"Item mod slot {occupiedSlotIds.First()} is occupied by at least one player's applied mod and cannot be deleted.");
                }
            }

            // Memoize each referenced item's current slot-id set so the Edit/Delete membership guard is an
            // O(1) lookup, not a per-change linear scan over the item's slots.
            var slotIdsByItem = new Dictionary<int, HashSet<int>>();
            HashSet<int> SlotIds(int itemId)
            {
                if (!slotIdsByItem.TryGetValue(itemId, out var ids))
                {
                    ids = _items.LookupItem(itemId)?.ItemModSlots.Select(s => s.Id).ToHashSet() ?? [];
                    slotIdsByItem[itemId] = ids;
                }
                return ids;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.ItemModSlot
                {
                    ItemId = item.ItemId,
                    ItemModSlotTypeId = (int)item.ItemModSlotTypeId,
                }),
                // Edit/Delete are guarded by the slot's membership in its stated owning item (mirroring the
                // other child-collection setters): a slot the item doesn't have is reconciled away, never a
                // silent EF 0-row update/delete.
                edit: item =>
                {
                    if (SlotIds(item.ItemId).Contains(item.Id))
                    {
                        _entityStore.Update(new Entities.ItemModSlot
                        {
                            Id = item.Id,
                            ItemId = item.ItemId,
                            ItemModSlotTypeId = (int)item.ItemModSlotTypeId,
                        });
                    }
                },
                delete: item =>
                {
                    if (SlotIds(item.ItemId).Contains(item.Id))
                    {
                        _entityStore.Delete(new Entities.ItemModSlot
                        {
                            Id = item.Id,
                        });
                    }
                },
                key: item => item.Id,
                resourceName: "item mod slot");
        }

        public async Task<AdminSaveResult> SetTags(SetTagsData data, CancellationToken cancellationToken = default)
        {
            if (_items.LookupItem(data.Id) is null)
            {
                return AdminSaveResult.NotFound("Item");
            }

            await TagAssignmentReconciler.ReconcileAsync(
                _tags.GetTagIdsForItem(data.Id),
                _tags.GetExistingTagIds(data.TagIds),
                _entityStore,
                tagId => new Entities.ItemTag { ItemId = data.Id, TagId = tagId },
                cancellationToken);

            return AdminSaveResult.Success;
        }
    }
}
