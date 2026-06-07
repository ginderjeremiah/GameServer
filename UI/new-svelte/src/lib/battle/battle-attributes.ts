import { IBattlerAttribute, EAttribute } from '$lib/api';
import { normalizeText } from '$lib/common';
import {
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier
} from './attribute-modifier';
import { computeAttributes } from './attribute-collection';

const attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;

export class BattleAttributes {
	public attributes: number[];

	constructor(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.attributes = new Array<number>(attributesMaxId + 1);
		this.setData(attList, calcDerivedStats);
	}

	public setData(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.attributes.fill(0, 0, attributesMaxId + 1);

		if (calcDerivedStats) {
			const modifiers: AttributeModifier[] = [
				...attList.map((att) => ({
					attribute: att.attributeId,
					amount: att.amount,
					type: EModifierType.Additive,
					source: EAttributeModifierSource.AttributeDistribution
				})),
				...STATIC_ATTRIBUTE_MODIFIERS
			];
			const computed = computeAttributes(modifiers);
			for (const [attr, result] of computed) {
				this.attributes[attr] = result.total;
			}
			// DropBonus uses log10(Luck), which cannot be expressed as a constant
			// modifier in the pipeline; computed as a post-step once Luck is resolved.
			this.attributes[EAttribute.DropBonus] += Math.log10(this.attributes[EAttribute.Luck]);
		} else {
			for (const att of attList) {
				this.attributes[att.attributeId] += att.amount;
			}
		}
	}

	public getValue(attId: EAttribute) {
		return this.attributes[attId];
	}

	public getAttributeMap = (includeZeroes: boolean = false) => {
		return this.attributes
			.map((att, i) => ({ name: normalizeText(EAttribute[i]), value: att }))
			.filter((att) => att.value != 0 || includeZeroes);
	};
}
