import { IBattlerAttribute, EAttribute } from '$lib/api';
import { normalizeText } from '$lib/common';

const attributesMaxId = Object.values(EAttribute)[Object.values(EAttribute).length - 1] as number;

export class BattleAttributes {
	public attributes: number[];

	constructor(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.attributes = new Array<number>(attributesMaxId);
		this.setData(attList, calcDerivedStats);
	}

	public setData(attList: IBattlerAttribute[] = [], calcDerivedStats: boolean = true) {
		this.attributes.fill(0, 0, attributesMaxId);
		for (const att of attList) {
			this.attributes[att.attributeId] += att.amount;
		}

		if (calcDerivedStats) {
			this.calculateDerivedStats();
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

	private calculateDerivedStats = () => {
		this.attributes[EAttribute.MaxHealth] +=
			50 + 20 * this.attributes[EAttribute.Endurance] + 5 * this.attributes[EAttribute.Strength];
		this.attributes[EAttribute.Defense] +=
			2 + this.attributes[EAttribute.Endurance] + 0.5 * this.attributes[EAttribute.Agility];
		this.attributes[EAttribute.CooldownRecovery] +=
			0.4 * this.attributes[EAttribute.Agility] + 0.1 * this.attributes[EAttribute.Dexterity];
		this.attributes[EAttribute.DropBonus] += Math.log10(this.attributes[EAttribute.Luck]);
		// this.attributes[EAttribute.CriticalChance] = 0;
		// this.attributes[EAttribute.CriticalDamage] = 0;
		// this.attributes[EAttribute.DodgeChance] = 0;
		// this.attributes[EAttribute.BlockChance] = 0;
		// this.attributes[EAttribute.BlockReduction] = 0;
	};
}
