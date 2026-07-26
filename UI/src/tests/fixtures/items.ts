import { EItemCategory, ERarity, type IItem } from '$lib/api';

/* Shared `IItem` contract builder (#2433), following the `fixtures/skills.ts` convention.

   The target set is narrower than a `grep` for item-shaped literals suggests: only a suite that
   constructs a *typed* `IItem` pays the contract-drift tax. Two kinds of item literal are excluded on
   purpose — those that build the live `Item` domain object via `as unknown as Item` (the inventory
   screens) and those that seed an untyped `staticData` mock (the workbench `entities` suites).
   Neither sees a new required field, and filling their previously-`undefined` fields could change
   what a suite renders, so a `grep` still finding item literals under `src/tests/` is expected, not
   a gap.

   A third kind — a literal asserted `as IItem` — *does* claim the contract to its consumer while
   silencing the missing field, which opts it out of drift detection. #2447 folded those in
   (`challenge-unlocks`, `ChallengeTooltip`, `skill-provenance`, `offline-summary`); reintroducing
   such a cast re-opens the hole, so state why if you do. */

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
