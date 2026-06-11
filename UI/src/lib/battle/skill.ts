import { IAttributeMultiplier, ISkill, ISkillEffect } from '$lib/api';
import { Battler } from './battler';
import { calculateSkillDamage } from './battle-formulas';

export class Skill implements ISkill {
	id: number;
	name: string;
	baseDamage: number;
	damageMultipliers: IAttributeMultiplier[];
	effects: ISkillEffect[];
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
		this.effects = data.effects;
		this.description = data.description;
		this.cooldownMs = data.cooldownMs;
		this.iconPath = data.iconPath;
		this.owner = owner;
	}

	public calculateDamage() {
		return calculateSkillDamage(this, this.owner.attributes);
	}
}
