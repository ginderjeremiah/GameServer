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

/** A single timed skill-effect application active on a battler, as the UI renders it (the active-effect
 *  chips). Each application is its own entry — re-applying an effect stacks a new one — so it carries a
 *  per-battler unique {@link applicationId} (to match its modifier on expiry and key it stably in the
 *  chips) alongside the authored effect's id, the attribute/modifier it shifts, its full and remaining
 *  durations, and a render-only remaining duration interpolated between ticks for a smooth countdown —
 *  the chip analogue of {@link Skill.renderChargeTime}. The modifier instance itself is kept off this
 *  view (in {@link Battler.effectModifiers}) so the view can be a reactive `statify` projection without
 *  a reactive proxy breaking the reference identity {@link BattleAttributes.removeModifier} matches on. */
export interface ActiveEffectView {
	applicationId: number;
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

	/** The modifier each active effect application added to the attribute set, keyed by the application's
	 *  unique id (not the authored effect id — applications stack, so an effect id maps to many).
	 *  A private (`#`) field so `statify` leaves it — and the modifier references it holds —
	 *  non-reactive, keeping the reference identity {@link BattleAttributes.removeModifier} matches on
	 *  intact (a reactive array would deep-proxy its elements). */
	#effectModifiers = new Map<number, AttributeModifier>();

	/** Monotonic per-battler counter handing each effect application a unique {@link
	 *  ActiveEffectView.applicationId}, so stacked applications of the same effect stay individually
	 *  addressable for expiry and chip keying. */
	#nextApplicationId = 0;

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

	/** Applies `rawDamage` after subtracting flat Defense and the optional `blockReduction` (a second flat
	 *  reduction in the same clamp, passed only when an incoming hit is blocked), never below zero. */
	public takeDamage(rawDamage: number, blockReduction = 0) {
		const damage = applyDefense(rawDamage, this.attributes.getValue(EAttribute.Defense), blockReduction);
		this.currentHealth -= damage;
		this.isDead = this.currentHealth <= 0;
		return damage;
	}

	/** Applies one tick of damage-over-time from DamageTakenPerSecond (authored per second, scaled to
	 *  `timeDelta`). Unlike {@link takeDamage} it BYPASSES Defense; returns the damage dealt. */
	public applyDamageOverTime(timeDelta: number) {
		const damage = (this.attributes.getValue(EAttribute.DamageTakenPerSecond) * timeDelta) / 1000;
		this.currentHealth -= damage;
		this.isDead = this.currentHealth <= 0;
		return damage;
	}

	/** Applies one tick of heal-over-time from HealthRegenPerSecond (authored per second, scaled to
	 *  `timeDelta`), capped at MaxHealth. Returns the actual (post-cap) health restored. */
	public applyHealOverTime(timeDelta: number) {
		const heal = (this.attributes.getValue(EAttribute.HealthRegenPerSecond) * timeDelta) / 1000;
		const healed = Math.min(heal, this.attributes.getValue(EAttribute.MaxHealth) - this.currentHealth);
		if (healed > 0) {
			this.currentHealth += healed;
			// Keep the cached flag in sync with currentHealth, mirroring takeDamage/applyDamageOverTime and
			// the backend's always-live IsDead, so isDead is correct regardless of mutation ordering.
			this.isDead = this.currentHealth <= 0;
			return healed;
		}

		return 0;
	}

	/** Applies a timed skill `effect` to this battler, using the already-resolved `amount` as its
	 *  magnitude — the caster-attribute scaling is computed by {@link Skill.applyEffects} before this is
	 *  reached, so `amount` defaults to the unscaled authored amount only for the direct (test) callers
	 *  that don't scale. Each application STACKS — it adds its own modifier and its own timed entry, so
	 *  re-applying an already-active effect sums the magnitudes (additive amounts add, multiplicative
	 *  factors compound) and each application expires on its own schedule. A new modifier may shift
	 *  MaxHealth, so the health is re-clamped. */
	public applyEffect(effect: ISkillEffect, amount: number = effect.amount): void {
		const applicationId = this.#nextApplicationId++;
		const modifier: AttributeModifier = {
			attribute: effect.attributeId,
			amount,
			type: effect.modifierTypeId,
			source: EAttributeModifierSource.SkillEffect
		};
		this.attributes.addModifier(modifier);
		this.#effectModifiers.set(applicationId, modifier);
		this.activeEffects.push({
			applicationId,
			sourceId: effect.id,
			attribute: effect.attributeId,
			modifierType: effect.modifierTypeId,
			amount,
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
				const modifier = this.#effectModifiers.get(active.applicationId);
				if (modifier) {
					this.attributes.removeModifier(modifier);
					this.#effectModifiers.delete(active.applicationId);
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

		// Re-arm every skill to the battle-start baseline. The data path's fillSelectedSkills already
		// produced fresh (charge-0) skills; the data-less re-arm (an idle re-spawn with an unchanged
		// loadout, #811) keeps the existing skills, so their charges must be zeroed here or the next
		// fight would inherit the previous battle's accrued cooldowns.
		for (const skill of this.skills) {
			if (skill) {
				skill.chargeTime = 0;
				skill.renderChargeTime = 0;
			}
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
