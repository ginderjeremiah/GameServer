import { Skill } from './skill';
import { BattleAttributes } from './battle-attributes';
import { mitigateDamage, resistanceTotal, cooldownMultiplier } from './battle-formulas';
import { dotAccumulators } from './damage-types';
import { isFielded } from './loadout';
import { IBattlerAttribute, ISkillEffect, EAttribute, EDamageType, EModifierType } from '$lib/api';
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

	/** The skill this battler ripostes with when it parries an incoming hit (#1457) — the equipped
	 *  weapon's signature (the virtual fists' punch bare-handed), resolved once at battler assembly like
	 *  the weapon-match gate. `undefined` when no counter is resolvable (an unauthored punch, or an enemy
	 *  battler — enemies never parry), in which case a parry negates without a riposte. Mirrors the
	 *  backend `Battler.CounterSkill`. */
	public counterSkill: Skill | undefined;

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

	/** This battler's elapsed simulated time in ms, advanced one tick at a time by
	 *  {@link Battler.advanceEffects}. Mirrors the backend `Battler._elapsedMs`. Active effects store an
	 *  absolute expiry against this clock in {@link Battler.effectExpiry}, so expiry is a comparison rather
	 *  than a per-tick countdown. */
	#elapsedMs = 0;

	/** Each active attribute's shared absolute expiry (`elapsedMs` at which its stack lapses), keyed by
	 *  attribute — mirrors the backend `AttributeEffectStack.ExpiresAtMs`. Both modifier types on the same
	 *  attribute share one entry, matching {@link Battler.applyEffect}'s shared-expiry reset. Kept off the
	 *  reactive {@link Battler.activeEffects} views (a private field, like {@link Battler.effectModifiers})
	 *  since it is bookkeeping, not display data; `remainingMs` is derived from it for the views that need it. */
	#effectExpiry = new Map<EAttribute, number>();

	/** Live read of the CooldownRecovery-derived multiplier (mirrors the backend), so a
	 *  mid-battle CDR change takes effect on the next tick rather than being frozen at reset. */
	public get cdMultiplier(): number {
		return cooldownMultiplier(this.attributes);
	}

	constructor(
		battlerData?: BattlerData,
		additionalAtttributes?: IBattlerAttribute[],
		grantedSkillIds?: number[],
		additionalModifiers?: AttributeModifier[],
		equippedWeaponType?: EDamageType,
		counterSkillId?: number
	) {
		this.reset(
			battlerData,
			additionalAtttributes,
			grantedSkillIds,
			additionalModifiers,
			equippedWeaponType,
			counterSkillId
		);
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

	/** Applies an incoming `dealt` hit (already amplified and crit-multiplied) of `damageType` via
	 *  {@link mitigateDamage} — percentage resistance then the Toughness mitigation curve.
	 *  Returns the net damage dealt; a negative result (absorption) heals this battler, CAPPED
	 *  at MaxHealth (no overheal — matching {@link applyHealOverTime}). */
	public takeDamage(dealt: number, damageType: EDamageType) {
		const net = mitigateDamage(dealt, damageType, this.attributes);
		if (net < 0) {
			// Absorption: cap the heal at the remaining room to MaxHealth, and report the actual healed amount.
			const heal = this.capHealToRoom(-net);
			this.currentHealth += heal;
			return heal === 0 ? 0 : -heal;
		}
		this.currentHealth -= net;
		return net;
	}

	/** Caps `heal` to this battler's remaining room to MaxHealth, floored at 0 — never negative, and never a
	 *  negative zero when the room is fully exhausted. Shared by the three channels whose net effect can be a
	 *  heal — {@link takeDamage}'s direct-hit absorption, {@link applyDamageOverTime}'s aggregate DoT-absorption,
	 *  and {@link applyHealOverTime} — since the game has no overheal/shield concept regardless of source.
	 *  Mirrors the backend `Battler.CapHealToRoom`. */
	private capHealToRoom(heal: number): number {
		const room = this.attributes.getValue(EAttribute.MaxHealth) - this.currentHealth;
		const capped = Math.min(heal, room);
		return capped > 0 ? capped : 0;
	}

	/** Subtracts `amount` of reflected damage directly from this (attacking) battler's health, BYPASSING all of
	 *  its own mitigation (resistance and the Toughness curve) — the deterministic damage-reflection channel
	 *  (#1330). The caller resolves the amount (defender net × the defender's DamageReflection) and reflects only
	 *  a positive hit, so this is a raw health subtraction. Mirrors the backend `Battler.TakeReflectedDamage`. */
	public takeReflectedDamage(amount: number) {
		this.currentHealth -= amount;
	}

	/** Applies one tick of typed damage-over-time (#1320, Area C). Loops the DoT types in the fixed
	 *  {@link dotAccumulators} order, scaling each type's per-second accumulator to `timeDelta` and applying
	 *  this (defending) battler's resistance for that type SAMPLED LIVE —
	 *  `perSec * timeDelta/1000 * (1 - Σ applies(type).resistance)` — so a vulnerability debuff makes existing
	 *  DoTs hurt immediately. The caster's amplification was frozen into the accumulator at apply time
	 *  ({@link Skill.applyEffects}). Unlike {@link takeDamage} it BYPASSES the Toughness curve (resistance is its
	 *  only mitigation) and is never reflected (reflection is scoped to direct hits, #1330); returns the total
	 *  damage dealt. With no DoT authored every accumulator is 0, so the return is an exact 0. Mirrors the backend
	 *  `Battler.ApplyDamageOverTime`.
	 *
	 *  Each type's own tick is intentionally NOT floored at zero. DoT bypasses mitigation, so a tick goes
	 *  negative only through a deliberately authored negative accumulator or a resistance above 1 (absorption) —
	 *  a floor wouldn't prevent that, just silently rewrite it. But the AGGREGATE health change this call
	 *  realizes IS capped at the remaining room to MaxHealth when the summed total is negative — matching
	 *  {@link takeDamage}'s absorption cap and {@link applyHealOverTime} (no overheal/shield concept). */
	public applyDamageOverTime(timeDelta: number) {
		let dot = 0;
		for (const { type, accumulator } of dotAccumulators()) {
			const perSecond = this.attributes.getValue(accumulator);
			if (perSecond === 0) {
				continue;
			}
			dot += ((perSecond * timeDelta) / 1000) * (1 - resistanceTotal(type, this.attributes));
		}
		if (dot < 0) {
			// Aggregate absorption (net heal): cap the realized health change at the remaining room to
			// MaxHealth, consistent with takeDamage's absorption cap and applyHealOverTime.
			const heal = this.capHealToRoom(-dot);
			dot = heal === 0 ? 0 : -heal; // avoid returning -0 when the room is fully exhausted
		}
		this.currentHealth -= dot;
		return dot;
	}

	/** Applies one tick of heal-over-time from HealthRegenPerSecond (authored per second, scaled to
	 *  `timeDelta`), capped at MaxHealth. Returns the actual (post-cap) health restored. */
	public applyHealOverTime(timeDelta: number) {
		const heal = (this.attributes.getValue(EAttribute.HealthRegenPerSecond) * timeDelta) / 1000;
		const healed = this.capHealToRoom(heal);
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

		// Re-applying any effect on this attribute resets the whole stack's shared expiry to the new
		// application's duration (it may extend a longer-lived application or cut a shorter one short) —
		// mirrors the backend `Battler.applyEffect`'s `stack.ExpiresAtMs = _elapsedMs + effect.DurationMs`.
		this.#effectExpiry.set(effect.attributeId, this.#elapsedMs + effect.durationMs);
		for (const v of this.activeEffects) {
			if (v.attribute === effect.attributeId) {
				v.durationMs = effect.durationMs;
				v.remainingMs = effect.durationMs;
				v.renderRemainingMs = effect.durationMs;
			}
		}

		this.clampHealthToMaxHealth();
	}

	/** Advances this battler's simulated-time clock by `timeDelta` and removes any active effect whose
	 *  shared expiry (in {@link Battler.effectExpiry}) has been reached — its modifier removed and the
	 *  totals recomputed — keying expiry to the absolute {@link Battler.elapsedMs} clock rather than a
	 *  per-tick countdown. Mirrors the backend `Battler.AdvanceEffects`, so the two are algebraically
	 *  identical rather than merely value-equal under a fixed tick size. Called at the start of each tick
	 *  before any skill fires, so an effect influences exactly `durationMs / tickSize` ticks counting the
	 *  one it was applied on. */
	public advanceEffects(timeDelta: number) {
		// Advance the clock every tick, even with no active effects, so an effect applied on a later tick
		// still computes its absolute expiry from the correct elapsed time — mirrors the backend's
		// unconditional `_elapsedMs += ms` ahead of its early-return check.
		this.#elapsedMs += timeDelta;

		if (this.activeEffects.length === 0) {
			return;
		}

		let removedAny = false;
		for (let i = this.activeEffects.length - 1; i >= 0; i--) {
			const view = this.activeEffects[i];
			// Every active view's attribute has a stored expiry (set by applyEffect); the elapsedMs fallback
			// is defensive only, and treats a missing entry as already-expired rather than stuck forever.
			const expiresAtMs = this.#effectExpiry.get(view.attribute) ?? this.#elapsedMs;
			if (expiresAtMs <= this.#elapsedMs) {
				const key = effectModifierKey(view.attribute, view.modifierType);
				const modifier = this.#effectModifiers.get(key);
				if (modifier) {
					this.attributes.removeModifier(modifier);
					this.#effectModifiers.delete(key);
				}
				this.#effectExpiry.delete(view.attribute);
				this.activeEffects.splice(i, 1);
				removedAny = true;
			} else {
				view.remainingMs = expiresAtMs - this.#elapsedMs;
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
		additionalModifiers?: AttributeModifier[],
		equippedWeaponType?: EDamageType,
		counterSkillId?: number
	) {
		// Remove the active effects' modifiers, not just the bookkeeping — a data-less reset keeps the
		// existing attribute set, so leaving the modifiers would carry the previous battle's buffs over.
		for (const modifier of this.#effectModifiers.values()) {
			this.attributes.removeModifier(modifier);
		}
		this.#effectModifiers.clear();
		this.#effectExpiry.clear();
		// The backend constructs a fresh Battler (and so a fresh _elapsedMs) per battle; this instance is
		// reused across battles (#811), so its clock must be rewound explicitly here instead.
		this.#elapsedMs = 0;
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
			this.skills = this.fillSkills(battlerData, grantedSkillIds ?? [], equippedWeaponType);
			// The parry counter (#1457): resolved from the supplied id like the loadout skills — an
			// unresolvable id (an unauthored punch) or an absent one (an enemy battler) leaves no counter,
			// mirroring the backend BattleSnapshot.ToBattler resolution.
			const counterData = counterSkillId !== undefined ? staticData.skills?.[counterSkillId] : undefined;
			this.counterSkill = counterData ? new Skill(counterData, this) : undefined;
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
	 *  the loadout cap so the fight screen keeps its fixed slots), then the granted skills (the equipped items'
	 *  signatures — including the virtual-fists punch when bare-handed — already gathered in EEquipmentSlot
	 *  order by InventoryManager) — the whole sequence de-duplicated by id, first occurrence wins, then put
	 *  through the weapon-match gate. A weapon-leaf-typed skill (#1342) is fielded only when it matches
	 *  `equippedWeaponType`; weapon-agnostic types always pass. `equippedWeaponType` is undefined for an
	 *  ungated battler (an enemy fields its full authored loadout), making the gate a no-op there. An id that
	 *  resolves to no skill is skipped. Mirrors the backend `BattleSnapshot.GetBattleSkillIds` /
	 *  `BattleLoadout.OrderSkillIds` (`Distinct()` then the gate) so the two simulators field the same skills. */
	private fillSkills(battlerData: BattlerData, grantedSkillIds: number[], equippedWeaponType?: EDamageType) {
		const skillData = staticData.skills ?? [];
		const seen = new Set<number>();
		const skills: (Skill | undefined)[] = [];
		const fielded = (skill: Skill): boolean =>
			equippedWeaponType === undefined || isFielded(skill.primaryDamageType, equippedWeaponType);
		// De-dupe by id (first occurrence wins, like the backend's Distinct) BEFORE the gate, so a dimmed
		// selected skill that an item also grants stays dropped rather than slipping in via the grant.
		const addSkill = (skillId: number): void => {
			if (seen.has(skillId)) {
				return;
			}
			seen.add(skillId);
			const data = skillData[skillId];
			if (data) {
				const skill = new Skill(data, this);
				if (fielded(skill)) {
					skills.push(skill);
				}
			}
		};
		for (const skillId of battlerData.selectedSkills) {
			addSkill(skillId);
		}
		while (skills.length < MAX_SELECTED_SKILLS) {
			skills.push(undefined);
		}

		for (const skillId of grantedSkillIds) {
			addSkill(skillId);
		}
		return skills;
	}
}
