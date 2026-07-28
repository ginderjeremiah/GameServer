import { EItemCategory, ERarity, type IItem } from '$lib/api';

/* Shared `IItem` contract builder (#2433). See `docs/frontend.md` → Testing Guidelines for the
   contract-drift rule.

   Two kinds of item literal are excluded on purpose: those building the live `Item` domain object via
   `as unknown as Item` (the inventory screens), and those seeding an untyped `staticData` mock (the
   workbench `entities` suites). */

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
