/* Pure derivation for the create-character class picker: resolving a class's starter kit and
   signature passive into display strings. Kept store-free (reference sets are passed in) so it unit
   tests without the static-data store, mirroring the param-passing `$lib/common` display helpers. */

import { EEquipmentSlot, type IAttribute, type IClass, type IItem, type ISkill } from '$lib/api';
import { attributeCode, formatEffectMagnitude, formatNum } from '$lib/common';

export interface KitSkill {
	id: number;
	name: string;
}

export interface KitItem {
	itemId: number;
	slot: EEquipmentSlot;
	name: string;
}

/** A class's starter skills with names resolved from the skills reference set (index-addressable by
 *  id), degrading to a stable id label when the set hasn't loaded. */
export const resolveStarterSkills = (cls: IClass, skills?: ISkill[]): KitSkill[] =>
	cls.starterSkillIds.map((id) => ({ id, name: skills?.[id]?.name ?? `Skill #${id}` }));

/** A class's starter equipment with item names, weapon-first — the weapon carries the kit's signature
 *  skill, so it leads the preview. */
export const resolveStarterEquipment = (cls: IClass, items?: IItem[]): KitItem[] =>
	[...cls.starterEquipment]
		.sort(
			(a, b) =>
				Number(b.equipmentSlot === EEquipmentSlot.WeaponSlot) - Number(a.equipmentSlot === EEquipmentSlot.WeaponSlot)
		)
		.map((e) => ({
			itemId: e.itemId,
			slot: e.equipmentSlot,
			name: items?.[e.itemId]?.name ?? `Item #${e.itemId}`
		}));

/** A one-line summary of the signature passive, e.g. `+8 END` or `+8 END (+0.5 per INT)` for the
 *  attribute-scaled form. Attribute codes come from the `Attributes` reference set, falling back to
 *  the humanised enum name when it isn't loaded. */
export const passiveSummary = (cls: IClass, attributes?: IAttribute[]): string => {
	const code = attributeCode(cls.passiveAttributeId, attributes);
	const base = `${formatEffectMagnitude(cls.passiveModifierType, cls.passiveAmount)} ${code}`;
	if (cls.passiveScalingAttributeId == null || cls.passiveScalingAmount === 0) {
		return base;
	}
	const scaleCode = attributeCode(cls.passiveScalingAttributeId, attributes);
	return `${base} (+${formatNum(cls.passiveScalingAmount)} per ${scaleCode})`;
};
