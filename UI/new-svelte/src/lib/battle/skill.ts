import { ISkill } from '$lib/api';
import { Battler } from './battler.svelte';

export interface Skill extends ISkill {
	chargeTime: number;
	renderChargeTime: number;
	owner: Battler;
	calculateDamage: () => number;
}

export const newSkill = (data: ISkill, owner: Battler) => {
	const skill = {
		...data,
		chargeTime: 0,
		renderChargeTime: 0,
		owner
	} as Skill;
	skill.calculateDamage = () => {
		let dmg = skill.baseDamage;
		skill.damageMultipliers.forEach((dmgType) => {
			dmg += owner.attributes.getValue(dmgType.attributeId) * dmgType.multiplier;
		});
		return dmg;
	};

	return skill;
};
