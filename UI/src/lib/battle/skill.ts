import { IAttributeMultiplier, ISkill } from '$lib/api';
import { Battler } from './battler';

export class Skill implements ISkill {
	id: number;
	name: string;
	baseDamage: number;
	damageMultipliers: IAttributeMultiplier[];
	description: string;
	cooldownMs: number;
	iconPath: string;
	chargeTime = 0;
	renderChargeTime = 0;
	owner: Battler;

	constructor(data: ISkill, owner: Battler) {
		this.id = data.id;
		this.name = data.name;
		this.baseDamage = data.baseDamage;
		this.damageMultipliers = data.damageMultipliers;
		this.description = data.description;
		this.cooldownMs = data.cooldownMs;
		this.iconPath = data.iconPath;
		this.owner = owner;
	}

	public calculateDamage() {
		let dmg = this.baseDamage;
		this.damageMultipliers.forEach((dmgType) => {
			dmg += this.owner.attributes.getValue(dmgType.attributeId) * dmgType.multiplier;
		});
		return dmg;
	}
}
