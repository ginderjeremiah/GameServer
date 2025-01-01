import { Skill } from './skill';
import { BattleAttributes } from './battle-attributes';
import { IBattlerAttribute, EAttribute } from '$lib/api';
import { staticData } from '$stores';

interface BattlerData {
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	level: number;
	name: string;
}

const maxSkills = 4;
let battlerId = 0;

export class Battler {
	public id = battlerId++;
	public name = '';
	public level = 0;
	public currentHealth = 0;
	public attributes: BattleAttributes = new BattleAttributes();
	public skills: (Skill | undefined)[] = [];
	public maxSkills: typeof maxSkills = maxSkills;
	public cdMultiplier = 1;
	public isDead = true;

	constructor(battlerData?: BattlerData, additionalAtttributes?: IBattlerAttribute[]) {
		this.reset(battlerData, additionalAtttributes);
	}

	public advanceCooldowns(timeDelta: number) {
		const firedSkills: Skill[] = [];
		for (const skill of this.skills) {
			if (skill) {
				skill.chargeTime += timeDelta * this.cdMultiplier;
				if (skill.chargeTime >= skill.cooldownMs) {
					firedSkills.push(skill);
					skill.chargeTime = 0;
				}
			}
		}
		return firedSkills;
	}

	public updateRenderCooldowns(renderDelta: number) {
		for (const skill of this.skills) {
			if (skill) {
				skill.renderChargeTime = Math.min(
					skill.chargeTime + renderDelta * this.cdMultiplier,
					skill.cooldownMs
				);
			}
		}
	}

	public takeDamage(rawDamage: number) {
		let damage = rawDamage - this.attributes.getValue(EAttribute.Defense);
		if (damage < 0) {
			damage = 0;
		}
		this.currentHealth -= damage;
		this.isDead = this.currentHealth <= 0;
		return damage;
	}

	public reset(battlerData?: BattlerData, additionalAtttributes?: IBattlerAttribute[]) {
		if (battlerData) {
			const atts = additionalAtttributes
				? [...battlerData.attributes, ...additionalAtttributes]
				: battlerData.attributes;

			this.attributes.setData(atts);
			this.level = battlerData.level;
			this.name = battlerData.name;
			this.skills = this.fillSelectedSkills(battlerData);
		}

		this.currentHealth = this.attributes.getValue(EAttribute.MaxHealth);
		this.cdMultiplier = 1 + this.attributes.getValue(EAttribute.CooldownRecovery) / 100;
		this.isDead = false;
	}

	private fillSelectedSkills(battlerData: BattlerData) {
		const skillData = staticData.skills;
		const skills: (Skill | undefined)[] = battlerData.selectedSkills.map(
			(skillId) => new Skill(skillData[skillId], this)
		);
		while (skills.length < maxSkills) {
			skills.push(undefined);
		}
		return skills;
	}
}
