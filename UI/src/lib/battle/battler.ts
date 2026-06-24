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

/** One contributing skill-effect source folded into an {@link ActiveEffectView}, for the chip tooltip's
 *  per-source breakdown. Stacking is unbounded, so applications are aggregated by their authored effect
 *  id rather than kept individually — the count stays bounded by the distinct sources, not the stack depth. */
export interface ActiveEffectSource {
	/** The authored skill-effect id (the chips resolve it to a skill name for the tooltip). */
	sourceId: number;
	/** This source's folded contribution (additive amounts summed, multiplicative factors compounded). */
	amount: number;
	/** How many applications from this source are folded in. */
	count: number;
}

/** The active timed effects on one (attribute, modifier type), as the UI renders it (one active-effect
 *  chip). Effects STACK, but every application on an attribute shares one expiry and folds into a single
 *  combined modifier, so the view is an aggregate — not one entry per application — keeping it (and the
 *  per-tick bookkeeping behind it) bounded no matter how deep a buff re-applies. {@link totalAmount} is
 *  the combined magnitude, {@link count} the number of folded applications (the chip's count badge), and
 *  {@link sources} the per-source breakdown for the tooltip. All views on the same attribute share one
 *  expiry, so their {@link remainingMs} move together (re-applying any of them resets them all — see
 *  {@link Battler.applyEffect}). {@link renderRemainingMs} is a render-only copy interpolated between
 *  ticks for a smooth countdown — the chip analogue of {@link Skill.renderChargeTime}. The combined
 *  modifier instances are kept off this view (in {@link Battler.effectModifiers}) so the view can be a
 *  reactive `statify` projection without a reactive proxy breaking the reference identity
 *  {@link BattleAttributes.removeModifier} matches on. */
export interface ActiveEffectView {
	attribute: EAttribute;
	modifierType: EModifierType;
	totalAmount: number;
	count: number;
	durationMs: number;
	remainingMs: number;
	renderRemainingMs: number;
	sources: ActiveEffectSource[];
}

/** Keys a combined effect modifier (for swap-on-apply and remove-on-expiry) by the attribute + modifier
 *  type it folds — the granularity at which stacked applications collapse into one modifier. */
const effectModifierKey = (attribute: EAttribute, type: EModifierType): string => `${attribute}:${type}`;

/** Folds one application's magnitude into the per-source breakdown for an {@link ActiveEffectView},
 *  aggregating by the authored effect id (additive amounts summed, multiplicative factors compounded) so
 *  the tooltip lists one row per source skill rather than one per unbounded application. */
const foldSourceContribution = (
	sources: ActiveEffectSource[],
	sourceId: number,
	amount: number,
	isMultiplicative: boolean
): void => {
	const source = sources.find((s) => s.sourceId === sourceId);
	if (source) {
		source.amount = isMultiplicative ? source.amount * amount : source.amount + amount;
		source.count++;
	} else {
		sources.push({ sourceId, amount, count: 1 });
	}
};

let battlerId = 0;

export class Battler {
	public id = battlerId++;
	public name = '';
	public level = 0;
	public currentHealth = 0;
	public attributes: BattleAttributes = new BattleAttributes();
	public skills: (Skill | undefined)[] = [];

	/** Live-derived death state, mirroring the backend's `IsDead => CurrentHealth <= 0` getter so the two
	 *  simulators agree by construction. Deriving it (rather than caching a flag re-synced at every
	 *  currentHealth mutation) removes the whole "forgot to re-sync" class of parity drift — notably the
	 *  MaxHealth-debuff clamp path, which lowers health without going through a damage mutation. */
	public get isDead(): boolean {
		return this.currentHealth <= 0;
	}

	/** The active timed effects on this battler, folded to one {@link ActiveEffectView} per (attribute,
	 *  modifier type), as a reactive (`statify`) projection the active-effect chips render. Display-only
	 *  data: it never carries a modifier reference, so making it reactive is safe (see
	 *  {@link ActiveEffectView}). */
	public activeEffects: ActiveEffectView[] = [];

	/** The single combined modifier each active (attribute, modifier type) added to the attribute set,
	 *  keyed by {@link effectModifierKey}. Applications stack into this one modifier rather than each
	 *  adding their own, so a persistently re-applied buff stays O(1) instead of growing one modifier per
	 *  fire. A private (`#`) field so `statify` leaves it — and the modifier references it holds —
	 *  non-reactive, keeping the reference identity {@link BattleAttributes.removeModifier} matches on
	 *  intact (a reactive array would deep-proxy its elements). */
	#effectModifiers = new Map<string, AttributeModifier>();

	/** Live read of the CooldownRecovery-derived multiplier (mirrors the backend), so a
	 *  mid-battle CDR change takes effect on the next tick rather than being frozen at reset. */
	public get cdMultiplier(): number {
		return cooldownMultiplier(this.attributes);
	}

	constructor(
		battlerData?: BattlerData,
		additionalAtttributes?: IBattlerAttribute[],
		grantedSkillIds?: number[],
		additionalModifiers?: AttributeModifier[]
	) {
		this.reset(battlerData, additionalAtttributes, grantedSkillIds, additionalModifiers);
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
		return damage;
	}

	/** Applies one tick of damage-over-time from DamageTakenPerSecond (authored per second, scaled to
	 *  `timeDelta`). Unlike {@link takeDamage} it BYPASSES Defense; returns the damage dealt.
	 *
	 *  Intentionally NOT floored at zero, unlike {@link takeDamage}: that floor exists only so Defense
	 *  mitigation can't drive net damage below zero and turn a hit into a heal. DoT has no mitigation step, so a
	 *  tick is negative only if a negative DamageTakenPerSecond is deliberately authored — and a floor wouldn't
	 *  prevent that, just silently rewrite it. Authored healing belongs in the capped {@link applyHealOverTime}
	 *  channel instead. */
	public applyDamageOverTime(timeDelta: number) {
		const damage = (this.attributes.getValue(EAttribute.DamageTakenPerSecond) * timeDelta) / 1000;
		this.currentHealth -= damage;
		return damage;
	}

	/** Applies one tick of heal-over-time from HealthRegenPerSecond (authored per second, scaled to
	 *  `timeDelta`), capped at MaxHealth. Returns the actual (post-cap) health restored. */
	public applyHealOverTime(timeDelta: number) {
		const heal = (this.attributes.getValue(EAttribute.HealthRegenPerSecond) * timeDelta) / 1000;
		const healed = Math.min(heal, this.attributes.getValue(EAttribute.MaxHealth) - this.currentHealth);
		if (healed > 0) {
			this.currentHealth += healed;
			return healed;
		}

		return 0;
	}

	/** Applies a timed skill `effect` to this battler, using the already-resolved `amount` as its
	 *  magnitude — the caster-attribute scaling is computed by {@link Skill.applyEffects} before this is
	 *  reached, so `amount` defaults to the unscaled authored amount only for the direct (test) callers
	 *  that don't scale. Each application STACKS: its magnitude folds into the attribute's single combined
	 *  modifier for the effect's type (additive amounts add, multiplicative factors compound). All active
	 *  applications on the SAME attribute share a single expiry: re-applying any effect on that attribute
	 *  resets the whole stack to this application's duration, so it expires together with no independent
	 *  per-portion expirations (#992 / #740). A new modifier may shift MaxHealth, so the health is
	 *  re-clamped. */
	public applyEffect(effect: ISkillEffect, amount: number = effect.amount): void {
		const isMultiplicative = effect.modifierTypeId === EModifierType.Multiplicative;
		let view = this.activeEffects.find(
			(v) => v.attribute === effect.attributeId && v.modifierType === effect.modifierTypeId
		);

		// Fold the application into the combined magnitude for this (attribute, type).
		const combinedAmount = view ? (isMultiplicative ? view.totalAmount * amount : view.totalAmount + amount) : amount;

		// Swap the single combined modifier in the attribute set (remove the old, add the new) so it holds at
		// most one effect modifier per (attribute, type) regardless of stack depth — mirroring the backend.
		const key = effectModifierKey(effect.attributeId, effect.modifierTypeId);
		const existing = this.#effectModifiers.get(key);
		if (existing) {
			this.attributes.removeModifier(existing);
		}
		const modifier: AttributeModifier = {
			attribute: effect.attributeId,
			amount: combinedAmount,
			type: effect.modifierTypeId,
			source: EAttributeModifierSource.SkillEffect
		};
		this.attributes.addModifier(modifier);
		this.#effectModifiers.set(key, modifier);

		if (view) {
			view.totalAmount = combinedAmount;
			view.count++;
			view.durationMs = effect.durationMs;
			foldSourceContribution(view.sources, effect.id, amount, isMultiplicative);
		} else {
			view = {
				attribute: effect.attributeId,
				modifierType: effect.modifierTypeId,
				totalAmount: combinedAmount,
				count: 1,
				durationMs: effect.durationMs,
				remainingMs: effect.durationMs,
				renderRemainingMs: effect.durationMs,
				sources: [{ sourceId: effect.id, amount, count: 1 }]
			};
			this.activeEffects.push(view);
		}

		// Re-applying any effect on this attribute resets the whole stack's shared remaining to the new
		// application's duration (it may extend a longer-lived application or cut a shorter one short). The
		// backend mirror keys this off an absolute ExpiresAtMs clock; under the fixed tick size the two expire
		// on the same tick (see advanceEffects).
		for (const v of this.activeEffects) {
			if (v.attribute === effect.attributeId) {
				v.remainingMs = effect.durationMs;
				v.renderRemainingMs = effect.durationMs;
			}
		}

		this.clampHealthToMaxHealth();
	}

	/** Advances every active effect by `timeDelta`, removing any whose duration has elapsed (its modifier
	 *  is removed and the totals recomputed). Called at the start of each tick before any skill fires, so
	 *  an effect influences exactly `durationMs / tickSize` ticks counting the one it was applied on.
	 *
	 *  Parity note: this decrements `remainingMs` per tick where the backend (`Battler.AdvanceEffects`)
	 *  instead keys expiry to an absolute `_elapsedMs` clock. The two are **value-equal only under the
	 *  current fixed tick size** — they are not algebraically identical under any future variable-tick or
	 *  fractional-accumulation change, so they must stay in lockstep (or the FE be converted to the
	 *  backend's absolute-expiry model). The mirrored parity matrix covers the apply→expire→re-apply
	 *  cycle where the two bookkeeping models are most likely to drift by a tick. */
	public advanceEffects(timeDelta: number) {
		if (this.activeEffects.length === 0) {
			return;
		}

		let removedAny = false;
		for (let i = this.activeEffects.length - 1; i >= 0; i--) {
			const view = this.activeEffects[i];
			view.remainingMs -= timeDelta;
			if (view.remainingMs <= 0) {
				const key = effectModifierKey(view.attribute, view.modifierType);
				const modifier = this.#effectModifiers.get(key);
				if (modifier) {
					this.attributes.removeModifier(modifier);
					this.#effectModifiers.delete(key);
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

	public reset(
		battlerData?: BattlerData,
		additionalAtttributes?: IBattlerAttribute[],
		grantedSkillIds?: number[],
		additionalModifiers?: AttributeModifier[]
	) {
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

			// Proficiency bonuses ride the modifier pipeline (additive/multiplicative by their type), not the
			// flat base data, so they compose through computeAttributes exactly like the backend's
			// AttributeCollection — the proficiency parity surface (#982 area E). They are handed to setData so
			// they sit with the base set BEFORE the static engine modifiers, matching the backend's additive
			// accumulation order exactly (#1189); appending them afterwards would diverge on attributes that
			// carry a static additive base (e.g. MaxHealth).
			this.attributes.setData(atts, true, additionalModifiers ?? []);
			this.level = battlerData.level;
			this.name = battlerData.name;
			this.skills = this.fillSkills(battlerData, grantedSkillIds ?? []);
		}

		// Re-arm every skill to the battle-start baseline. The data path's fillSkills already
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
	}

	/** Assembles the battler's loadout: the player-selected skills first (in their chosen order, padded to
	 *  the loadout cap so the fight screen keeps its fixed slots), then the item-granted skills (already
	 *  gathered in EEquipmentSlot order) de-duplicated by id against the selected skills and each other —
	 *  first occurrence wins. Mirrors the backend `BattleSnapshot.ToBattler` / `BattleLoadout.OrderSkillIds`
	 *  assembly so the two simulators field the same skills (battle parity). */
	private fillSkills(battlerData: BattlerData, grantedSkillIds: number[]) {
		const skillData = staticData.skills ?? [];
		const skills: (Skill | undefined)[] = battlerData.selectedSkills.map(
			(skillId) => new Skill(skillData[skillId], this)
		);
		while (skills.length < MAX_SELECTED_SKILLS) {
			skills.push(undefined);
		}

		const seen = new Set(battlerData.selectedSkills);
		for (const skillId of grantedSkillIds) {
			if (!seen.has(skillId)) {
				seen.add(skillId);
				skills.push(new Skill(skillData[skillId], this));
			}
		}
		return skills;
	}
}
