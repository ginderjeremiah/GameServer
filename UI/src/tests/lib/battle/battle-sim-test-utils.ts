import { Battler, newItem } from '$lib/battle';
import {
	EAttribute,
	EItemCategory,
	ERarity,
	type EModifierType,
	type ESkillEffectTarget,
	type EItemModType,
	type IAppliedModModel,
	type IAttributeMultiplier,
	type IBattlerAttribute,
	type IItem,
	type IItemMod,
	type ISkill,
	type ISkillEffect
} from '$lib/api';

/**
 * Shared helpers for the battle-simulation tests. They build the **real**
 * production `Battler`/`Skill` objects (the same ones the live BattleEngine
 * drives) from raw scenario inputs, so the tests exercise the actual per-tick
 * arithmetic instead of a hand-rolled copy.
 *
 * Each test file must `vi.mock('$stores')` with a `staticData.skills` getter
 * returning its own mock registry array, then pass that array to
 * {@link battlerFactory}; `makeBattler` registers each skill spec into it so the
 * Battler resolves its skills by id exactly as it does in the running game.
 */

/** A skill's raw definition, before it is registered and given an id. */
export interface SkillSpec {
	baseDamage: number;
	cooldownMs: number;
	multipliers: IAttributeMultiplier[];
	effects: ISkillEffect[];
}

export const makeSkill = (
	baseDamage: number,
	cooldownMs: number,
	multipliers: IAttributeMultiplier[] = [],
	effects: ISkillEffect[] = []
): SkillSpec => ({ baseDamage, cooldownMs, multipliers, effects });

/** A timed skill effect, mirroring the backend parity test's MakeEffect helper. The scaling fields
 *  default to no scaling (a `scalingAmount` of 0), so existing scenarios are unaffected. */
export const makeEffect = (
	id: number,
	target: ESkillEffectTarget,
	attributeId: EAttribute,
	modifierTypeId: EModifierType,
	amount: number,
	durationMs: number,
	scalingAttributeId: EAttribute = EAttribute.Strength,
	scalingAmount = 0
): ISkillEffect => ({ id, target, attributeId, modifierTypeId, amount, durationMs, scalingAttributeId, scalingAmount });

/**
 * Binds a `makeBattler` builder to a mock skill registry (the array a test's
 * mocked `staticData.skills` returns). `makeBattler` registers each skill spec
 * into the registry, then builds a real Battler from raw stat allocations whose
 * `selectedSkills` reference those registered ids. The optional `equipment` is
 * passed through as the Battler's additional attributes — exactly how the live
 * game feeds `InventoryManager.equipmentStats` to the player Battler — so a
 * scenario can layer equipped item + applied-mod attributes on top of the raw
 * stat allocations before derived stats are computed.
 */
export function battlerFactory(registry: ISkill[]) {
	return (
		attrs: { id: EAttribute; amount: number }[],
		skills: SkillSpec[],
		equipment?: IBattlerAttribute[]
	): Battler => {
		const selectedSkills = skills.map((spec) => {
			const id = registry.length;
			registry.push({
				id,
				name: `Skill ${id}`,
				baseDamage: spec.baseDamage,
				cooldownMs: spec.cooldownMs,
				damageMultipliers: spec.multipliers,
				effects: spec.effects,
				description: '',
				iconPath: ''
			});
			return id;
		});

		return new Battler(
			{
				name: 'Battler',
				level: 1,
				selectedSkills,
				attributes: attrs.map((a) => ({ attributeId: a.id, amount: a.amount }))
			},
			equipment
		);
	};
}

/** A single applied mod's raw definition, before it is registered and given an id. */
export interface ModSpec {
	type: EItemModType;
	attributes: IBattlerAttribute[];
}

/** An equipped item's raw definition, before it (and its mods) are registered. */
export interface ItemSpec {
	attributes: IBattlerAttribute[];
	mods: ModSpec[];
}

/**
 * Binds a `makeEquipment` builder to mock item + itemMod registries (the arrays a
 * test's mocked `staticData.items` / `staticData.itemMods` return). It registers
 * the item and each applied mod, builds the real production `Item` via
 * {@link newItem} — exercising the item + applied-mod attribute merge — and
 * returns the flattened equipment stats exactly as the live
 * `InventoryManager.equipmentStats` does (the item's own attributes followed by
 * every applied mod's attributes), ready to feed a Battler as its equipment.
 */
export function equipmentFactory(itemRegistry: IItem[], itemModRegistry: IItemMod[]) {
	return (spec: ItemSpec): IBattlerAttribute[] => {
		const appliedMods: IAppliedModModel[] = spec.mods.map((mod, slotIndex) => {
			const id = itemModRegistry.length;
			itemModRegistry.push({
				id,
				name: `Mod ${id}`,
				description: '',
				itemModTypeId: mod.type,
				rarityId: ERarity.Common,
				attributes: mod.attributes,
				tags: []
			});
			return { itemModId: id, itemModSlotId: slotIndex };
		});

		const itemId = itemRegistry.length;
		itemRegistry.push({
			id: itemId,
			name: `Item ${itemId}`,
			description: '',
			itemCategoryId: EItemCategory.Accessory,
			rarityId: ERarity.Common,
			iconPath: '',
			attributes: spec.attributes,
			modSlots: [],
			tags: []
		});

		const item = newItem({ itemId, equipped: true, favorite: false, appliedMods });
		// The item was just registered, so resolution is guaranteed; assert it to satisfy the nullable
		// return and surface a clear failure if that invariant is ever broken.
		if (!item) {
			throw new Error(`equipmentFactory failed to resolve just-registered item ${itemId}`);
		}

		// Mirror InventoryManager.equipmentStats: the equipped item's own
		// attributes followed by every applied mod's attributes.
		return [...item.attributes, ...item.appliedMods.flatMap((mod) => mod.attributes)];
	};
}
