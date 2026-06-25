/* Pure display derivation for the create-character class picker. The class data itself (with starter
   skill/item names already resolved) comes from the bespoke `Login/CharacterCreationData` payload, so
   this only orders equipment and formats the signature passive. Kept store-free (attribute reference
   data is passed in) so it unit tests without the static-data store, mirroring the param-passing
   `$lib/common` display helpers. */

import { EEquipmentSlot, type IAttribute, type ICreatableClass, type ICreatableClassEquipment } from '$lib/api';
import { attributeCode, formatEffectMagnitude, formatNum } from '$lib/common';

/** A class's starter equipment ordered weapon-first — the weapon carries the kit's signature skill, so
 *  it leads the preview. */
export const weaponFirst = (equipment: ICreatableClassEquipment[]): ICreatableClassEquipment[] =>
	[...equipment].sort(
		(a, b) =>
			Number(b.equipmentSlot === EEquipmentSlot.WeaponSlot) - Number(a.equipmentSlot === EEquipmentSlot.WeaponSlot)
	);

/** A one-line summary of the signature passive, e.g. `+8 END` or `+8 END (+0.5 per INT)` for the
 *  attribute-scaled form. Attribute codes come from the `Attributes` reference set, falling back to the
 *  humanised enum name when it isn't loaded (the create-character screen may run before reference data). */
export const passiveSummary = (cls: ICreatableClass, attributes?: IAttribute[]): string => {
	const code = attributeCode(cls.passiveAttributeId, attributes);
	const base = `${formatEffectMagnitude(cls.passiveModifierType, cls.passiveAmount)} ${code}`;
	if (cls.passiveScalingAttributeId == null || cls.passiveScalingAmount === 0) {
		return base;
	}
	const scaleCode = attributeCode(cls.passiveScalingAttributeId, attributes);
	return `${base} (+${formatNum(cls.passiveScalingAmount)} per ${scaleCode})`;
};
