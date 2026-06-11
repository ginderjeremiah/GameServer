import { Skill } from './skill';
import { BattleAttributes } from './battle-attributes';
import { applyDefense, cooldownMultiplier } from './battle-formulas';
import { IBattlerAttribute, ISkillEffect, EAttribute, EModifierType } from '$lib/api';
import { EAttributeModifierSource, type AttributeModifier } from './attribute-modifier';
import { MAX_SELECTED_SKILLS } from '$lib/api/types/game-constants';
import { staticData } from '$stores';

interface BattlerData {
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	level: number;
	name: string;
}

/** A timed skill effect active on a battler, as the UI renders it (the active-effect chips). Holds the
 *  authored effect's id (the refresh key), the attribute/modifier it shifts, its full and remaining
 *  durations, and a render-only remaining duration interpolated between ticks for a smooth countdown —
 *  the chip analogue of {@link Skill.renderChargeTime}. The modifier instance itself is kept off this
 *  view (in {@link Battler.effectModifiers}) so the view can be a reactive `statify` projection without
 *  a reactive proxy breaking the reference identity {@link BattleAttributes.removeModifier} matches on. */
export interface ActiveEffectView {
	sourceId: number;
	attribute: EAttribute;
	modifierType: EModifierType;
	amount: number;
	durationMs: number;
	remainingMs: number;
	renderRemainingMs: number;
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

	/** The timed skill effects currently on this battler, as a reactive (`statify`) projection the
	 *  active-effect chips render. Display-only data: it never carries a modifier reference, so making
	 *  it reactive is safe (see {@link ActiveEffectView}). */
	public activeEffects: ActiveEffectView[] = [];

	/** The modifier each active effect added to the attribute set, keyed by the effect's authored id.
	 *  A private (`#`) field so `statify` leaves it — and the modifier references it holds —
	 *  non-reactive, keeping the reference identity {@link BattleAttributes.removeModifier} matches on
	 *  intact (a reactive array would deep-proxy its elements). */
	#effectModifiers = new Map<number, AttributeModifier>();

	/** Live read of the CooldownRecovery-derived multiplier (mirrors the backend), so a
	 *  mid-battle CDR change takes effect on the next tick rather than being frozen at reset. */
	public get cdMultiplier(): number {
		return cooldownMultiplier(this.attributes);
	}

	constructor(battlerData?: BattlerData, additionalAtttributes?: IBattlerAttribute[]) {
		this.reset(battlerData, additionalAtttributes);
	}

	/** Advances each skill's charge by `timeDelta * cdMultiplier` **in loadout order**, invoking `onFire`
	 *  for each skill that becomes ready as soon as it fires — before the next slot accrues. Because
	 *  `cdMultiplier` is read live per slot, an earlier slot's effect (e.g. a self CooldownRecovery buff
	 *  applied in `onFire`) influences a later slot's accrual on the same tick, mirroring the backend's
	 *  per-slot `BattleSkill.Update`. */
	public advanceCooldowns(timeDelta: number, onFire: (skill: Skill) => void) {
		for (const skill of this.skills) {
			if (skill) {
				skill.chargeTime += timeDelta * this.cdMultiplier;
				if (skill.chargeTime >= skill.cooldownMs) {
					skill.chargeTime = 0;
					onFire(skill);
				}
			}
		}
	}

	public updateRenderCooldowns(renderDelta: number) {
		for (const skill of this.skills) {
			if (skill) {
				skill.renderChargeTime = Math.min(skill.chargeTime + renderDelta * this.cdMultiplier, skill.cooldownMs);
			}
		}
	}

	/** Interpolates each active effect's render-only remaining duration toward the next logical tick,
	 *  mirroring {@link updateRenderCooldowns}, so the chip countdown depletes smoothly between the
	 *  40ms logical ticks rather than stepping. Display-only; never touches the parity-relevant
	 *  {@link ActiveEffectView.remainingMs}. */
	public updateRenderEffects(renderDelta: number) {
		for (const effect of this.activeEffects) {
			effect.renderRemainingMs = Math.max(effect.remainingMs - renderDelta, 0);
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
		for (const active of this.activeEffects) {
			if (active.sourceId === effect.id) {
				active.remainingMs = effect.durationMs;
				active.renderRemainingMs = effect.durationMs;
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
		this.#effectModifiers.set(effect.id, modifier);
		this.activeEffects.push({
			sourceId: effect.id,
			attribute: effect.attributeId,
			modifierType: effect.modifierTypeId,
			amount: effect.amount,
			durationMs: effect.durationMs,
			remainingMs: effect.durationMs,
			renderRemainingMs: effect.durationMs
		});
		this.clampHealthToMaxHealth();
	}

	/** Advances every active effect by `timeDelta`, removing any whose duration has elapsed (its modifier
	 *  is removed and the totals recomputed). Called at the start of each tick before any skill fires, so
	 *  an effect influences exactly `durationMs / tickSize` ticks counting the one it was applied on. */
	public advanceEffects(timeDelta: number) {
		if (this.activeEffects.length === 0) {
			return;
		}

		let removedAny = false;
		for (let i = this.activeEffects.length - 1; i >= 0; i--) {
			const active = this.activeEffects[i];
			active.remainingMs -= timeDelta;
			if (active.remainingMs <= 0) {
				const modifier = this.#effectModifiers.get(active.sourceId);
				if (modifier) {
					this.attributes.removeModifier(modifier);
					this.#effectModifiers.delete(active.sourceId);
				}
				this.activeEffects.splice(i, 1);
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
		// Remove the active effects' modifiers, not just the bookkeeping — a data-less reset keeps the
		// existing attribute set, so leaving the modifiers would carry the previous battle's buffs over.
		for (const modifier of this.#effectModifiers.values()) {
			this.attributes.removeModifier(modifier);
		}
		this.#effectModifiers.clear();
		this.activeEffects = [];
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
