import { Skill } from './skill';
import { BattleAttributes } from './battle-attributes';
import { applyDefense, cooldownMultiplier } from './battle-formulas';
import { IBattlerAttribute, ISkillEffect, EAttribute } from '$lib/api';
import { EAttributeModifierSource, type AttributeModifier } from './attribute-modifier';
import { MAX_SELECTED_SKILLS } from '$lib/api/types/game-constants';
import { staticData } from '$stores';

interface BattlerData {
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	level: number;
	name: string;
}

/** A timed skill effect active on a battler: the authored effect's id (the refresh key), the modifier it
 *  added to the attribute set (kept for identity removal on expiry), and the remaining duration in ms. */
interface ActiveEffect {
	sourceId: number;
	modifier: AttributeModifier;
	remainingMs: number;
}

let battlerId = 0;

export class Battler {
	public id = battlerId++;
	public name = '';
	public level = 0;
	public currentHealth = 0;
	public attributes: BattleAttributes = new BattleAttributes();
	public skills: (Skill | undefined)[] = [];
	public isDead = true;

	/** The timed skill effects currently modifying this battler. A private (`#`) field so `statify`
	 *  leaves it (and the modifier references it holds) non-reactive — otherwise a reactive proxy would
	 *  break the reference identity {@link BattleAttributes.removeModifier} matches on. */
	#activeEffects: ActiveEffect[] = [];

	/** Live read of the CooldownRecovery-derived multiplier (mirrors the backend), so a
	 *  mid-battle CDR change takes effect on the next tick rather than being frozen at reset. */
	public get cdMultiplier(): number {
		return cooldownMultiplier(this.attributes);
	}

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
				skill.renderChargeTime = Math.min(skill.chargeTime + renderDelta * this.cdMultiplier, skill.cooldownMs);
			}
		}
	}

	public takeDamage(rawDamage: number) {
		const damage = applyDefense(rawDamage, this.attributes.getValue(EAttribute.Defense));
		this.currentHealth -= damage;
		this.isDead = this.currentHealth <= 0;
		return damage;
	}

	/** Applies a timed skill `effect` to this battler. Re-applying an already-active effect (matched by
	 *  its authored id) refreshes its remaining duration without adding a second modifier (no stacking);
	 *  a new effect adds a modifier and may shift MaxHealth, so the health is re-clamped. */
	public applyEffect(effect: ISkillEffect) {
		for (const active of this.#activeEffects) {
			if (active.sourceId === effect.id) {
				active.remainingMs = effect.durationMs;
				return;
			}
		}

		const modifier: AttributeModifier = {
			attribute: effect.attributeId,
			amount: effect.amount,
			type: effect.modifierTypeId,
			source: EAttributeModifierSource.SkillEffect
		};
		this.attributes.addModifier(modifier);
		this.#activeEffects.push({ sourceId: effect.id, modifier, remainingMs: effect.durationMs });
		this.clampHealthToMaxHealth();
	}

	/** Advances every active effect by `timeDelta`, removing any whose duration has elapsed (its modifier
	 *  is removed and the totals recomputed). Called at the start of each tick before any skill fires, so
	 *  an effect influences exactly `durationMs / tickSize` ticks counting the one it was applied on. */
	public advanceEffects(timeDelta: number) {
		if (this.#activeEffects.length === 0) {
			return;
		}

		let removedAny = false;
		for (let i = this.#activeEffects.length - 1; i >= 0; i--) {
			const active = this.#activeEffects[i];
			active.remainingMs -= timeDelta;
			if (active.remainingMs <= 0) {
				this.attributes.removeModifier(active.modifier);
				this.#activeEffects.splice(i, 1);
				removedAny = true;
			}
		}

		if (removedAny) {
			this.clampHealthToMaxHealth();
		}
	}

	/** Clamps currentHealth down to MaxHealth when an effect change has dropped the maximum below it; a
	 *  rise in MaxHealth leaves currentHealth untouched (no free healing). */
	private clampHealthToMaxHealth() {
		const maxHealth = this.attributes.getValue(EAttribute.MaxHealth);
		if (this.currentHealth > maxHealth) {
			this.currentHealth = maxHealth;
		}
	}

	public reset(battlerData?: BattlerData, additionalAtttributes?: IBattlerAttribute[]) {
		this.#activeEffects = [];
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
		this.isDead = false;
	}

	private fillSelectedSkills(battlerData: BattlerData) {
		const skillData = staticData.skills ?? [];
		const skills: (Skill | undefined)[] = battlerData.selectedSkills.map(
			(skillId) => new Skill(skillData[skillId], this)
		);
		while (skills.length < MAX_SELECTED_SKILLS) {
			skills.push(undefined);
		}
		return skills;
	}
}
