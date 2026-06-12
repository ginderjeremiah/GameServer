import { IAttributeMultiplier, ISkill, ISkillEffect, ESkillEffectTarget } from '$lib/api';
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

	/** Applies this skill's effects when it fires: each {@link ESkillEffectTarget.Self} effect to the
	 *  casting owner, each {@link ESkillEffectTarget.Opponent} effect to the given opponent. Called after
	 *  the skill's damage is dealt, so a self damage-buff never boosts its own carrying hit. The optional
	 *  `onApplied` callback — supplied only by the live engine, never the headless simulator — is invoked
	 *  for each *newly* applied (not refreshed) effect with the battler it landed on, so the combat log
	 *  can announce it. */
	public applyEffects(opponent: Battler, onApplied?: (effect: ISkillEffect, target: Battler) => void) {
		for (const effect of this.effects) {
			const target = effect.target === ESkillEffectTarget.Self ? this.owner : opponent;
			const applied = target.applyEffect(effect);
			if (applied && onApplied) {
				onApplied(effect, target);
			}
		}
	}
}
