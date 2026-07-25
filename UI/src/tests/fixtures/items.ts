import { EItemCategory, ERarity, type IItem } from '$lib/api';

/* Shared `IItem` contract builder (#2433), following the `fixtures/skills.ts` convention.

   The target set is narrower than a `grep` for item-shaped literals suggests: only seven modules
   actually construct a *typed* `IItem`, and they are the only ones that pay the contract-drift tax.
   Three groups are excluded on purpose:

   · The inventory screens (`EquipSlot`, `GridSlot`, `ItemDrawer`, `ItemTooltip`, `ModSlots`,
     `EquippedRail`, `Inventory`, `InventoryGrid`, `inventory-view`, `RewardTooltip`) build the live
     `Item` domain object via `as unknown as Item`, not the contract — a different shape with the
     merged mod totals attached.
   · `ChallengeTooltip`, `challenge-unlocks` and `offline-summary` assert deliberately *partial*
     literals to `IItem`, so a new required field never reaches them.
   · The workbench `reference`/`entities` suites and `engine.test.ts` seed literals into a
     `staticData: {} as any` mock, which type-checks nothing.

   Filling the previously-`undefined` fields of any of those could change what a suite renders, so a
   `grep` still finding item literals under `src/tests/` is expected, not a gap. */

/**
 * Builds an {@link IItem} reference-data entry for tests. Everything but the id is a neutral
 * placeholder so a suite states only what it asserts on.
 *
 * `itemCategoryId` defaults to `Accessory` — the categories that carry extra meaning (`Weapon`
 * implies a `weaponType`, the armour slots map to a specific `EEquipmentSlot`) would make the
 * default load-bearing. The optional fields (`grantedSkillId`, `weaponType`,
 * `requiredProficiencyId`, `retiredAt`) are left unset for the same reason: an unequipped,
 * ungated, un-retired item is the neutral case.
 */
export const makeItem = (overrides: Partial<IItem> & { id: number }): IItem => ({
	name: `Item ${overrides.id}`,
	description: '',
	itemCategoryId: EItemCategory.Accessory,
	rarityId: ERarity.Common,
	iconPath: '',
	attributes: [],
	modSlots: [],
	tags: [],
	requiredProficiencyLevel: 0,
	designerNotes: '',
	...overrides
});
