import type { Battler } from './battler';
import type { Skill } from './skill';
import type { Mulberry32 } from '$lib/engine/mulberry32';
import { EAttribute, type ISkillDamagePortion, type ISkillEffect } from '$lib/api';
import { amplifiedDamage } from './battle-formulas';

/** Resolves one direct hit by splitting `raw` across the skill's weighted `portions` (#1343): each portion
 *  takes `raw × weight ÷ Σweights` of the single raw hit (the weight total folded in fixed portion order, a
 *  parity contract) and runs the single-type pipeline — attacker amplification, then `critMultiplier` (the
 *  attacker's CriticalDamage on a crit, else 1, so one crit decision scales EVERY portion) and
 *  `executeMultiplier` (the Cull execute overlay, #1430 — 1 when unauthored or the target is at full health),
 *  then the defender's resistance + Toughness curve — under its own type; the per-portion nets are summed and
 *  returned. Each portion's absorption heal is capped at the defender's MaxHealth room AT THAT POINT in the
 *  fixed order, so the order is a parity contract. A single portion reduces byte-for-byte to the pre-feature
 *  single-typed hit. Mirrors the backend `BattleContext.DamageTarget` portion loop. */
function dealPortionedDamage(
	attacker: Battler,
	defender: Battler,
	raw: number,
	portions: ISkillDamagePortion[],
	critMultiplier: number,
	executeMultiplier: number = 1
): number {
	// Relies on the skill's damagePortions invariant (≥1 positive-weight portion, enforced by authoring
	// validation + backfill), so totalWeight is never 0 — no zero-division/empty-hit guard is needed.
	let totalWeight = 0;
	for (const portion of portions) {
		totalWeight += portion.weight;
	}
	let totalNet = 0;
	for (const portion of portions) {
		const dealt = amplifiedDamage((raw * portion.weight) / totalWeight, portion.type, attacker.attributes);
		totalNet += defender.takeDamage(dealt * critMultiplier * executeMultiplier, portion.type, attacker.level);
	}
	return totalNet;
}

/** Deterministic damage reflection (#1330): the `defender` returns its DamageReflection share of a direct
 *  hit's `netDamage` to the `attacker`, BYPASSING the attacker's mitigation. Only a positive net reflects (a
 *  dodged or absorbed hit returns nothing), and DoT is never routed here, so DoT is never reflected. Returns
 *  the amount reflected (0 when nothing was) so the live engine can log a reflected-damage line; the headless
 *  simulator ignores the return. Mirrors the backend `BattleContext.ReflectDamage`. */
function reflectDamage(attacker: Battler, defender: Battler, netDamage: number): number {
	if (netDamage <= 0) {
		return 0;
	}
	const reflection = defender.attributes.getValue(EAttribute.DamageReflection);
	if (reflection > 0) {
		const reflected = netDamage * reflection;
		attacker.takeReflectedDamage(reflected);
		return reflected;
	}
	return 0;
}

/**
 * A single skill activation produced by one battle tick: which skill fired, the
 * final damage it dealt after the defender's mitigation, and whether the
 * player (true) or the enemy (false) was the attacker. The crit/dodged/parried flags
 * carry the player-only roll outcomes for the combat log: `crit` on a player
 * attack, `dodged`/`parried` on an incoming enemy attack (#1457 — parry is checked
 * before dodge, so the two are mutually exclusive). `reflected` is the deterministic
 * damage (#1330) the defender returned to the attacker for this hit (0 when none),
 * so the live engine can log a reflected-damage line. A proc'd parry additionally
 * pushes a second activation for the counter fire (`byPlayer: true`, `skill` the
 * riposte's weapon signature) — see {@link fireCounter}. The live BattleEngine turns
 * these into combat-log messages; the headless BattleSimulator ignores them.
 */
export interface SkillActivation {
	skill: Skill;
	damage: number;
	byPlayer: boolean;
	crit: boolean;
	dodged: boolean;
	parried: boolean;
	reflected: number;
}

/**
 * Fires the defending player's riposte on a proc'd parry (#1457): the counter is the equipped weapon's
 * signature skill ({@link Battler.counterSkill}, resolved once at battler assembly), fired as damage only —
 * no cooldown consumed, no effects applied — through the same portioned-damage/reflect helpers a normal
 * player fire uses, so crit (the counter's own authored `criticalChance`, scaled by `CriticalChanceMultiplier`
 * like any skill), the Cull execute overlay, and enemy reflection of the counter all mirror the backend's
 * `BattleContext.FireCounter`/recursive `DamageTarget` call. Returns `undefined` when the player has no
 * resolvable counter skill (Parry is player-only, and the enemy never fires one).
 */
function fireCounter(player: Battler, enemy: Battler, rng: Mulberry32): SkillActivation | undefined {
	const counterSkill = player.counterSkill;
	if (!counterSkill) {
		return undefined;
	}
	// One crit draw for the counter fire, taken even at 0 authored chance — mirroring the ordinary per-fire
	// crit draw so the stream stays in lockstep regardless of whether the counter can crit.
	const crit =
		rng.next() < counterSkill.criticalChance * player.attributes.getValue(EAttribute.CriticalChanceMultiplier);
	const raw = counterSkill.calculateDamage();
	const critMultiplier = crit ? player.attributes.getValue(EAttribute.CriticalDamage) : 1;
	const maxHealth = enemy.attributes.getValue(EAttribute.MaxHealth);
	const missingHpFraction = Math.min(1, Math.max(0, (maxHealth - enemy.currentHealth) / maxHealth));
	const executeMultiplier = 1 + player.attributes.getValue(EAttribute.ExecuteBonus) * missingHpFraction;
	const damage = dealPortionedDamage(
		player,
		enemy,
		raw,
		counterSkill.damagePortions,
		critMultiplier,
		executeMultiplier
	);
	const reflected = reflectDamage(player, enemy, damage);
	return { skill: counterSkill, damage, byPlayer: true, crit, dodged: false, parried: false, reflected };
}

/** A skill effect application that landed during a tick (each application stacks), with the side it
 *  landed on. */
export interface AppliedEffect {
	effect: ISkillEffect;
	/** Whether the effect landed on the player (`true`) or the enemy (`false`). */
	onPlayer: boolean;
	/** The resolved (caster-scaled) magnitude that was applied — what the combat log should report. */
	amount: number;
}

/**
 * Optional per-tick observation sink the live {@link BattleEngine} passes to {@link battleStep} to
 * collect the log-worthy events a tick produces beyond its damage activations: the effects newly
 * applied this tick, and the damage/heal each side took from the end-of-tick DoT/HoT phase. The
 * headless {@link BattleSimulator} omits it, so the parity path computes and allocates nothing extra
 * and stays byte-identical to before. Reused across ticks by the caller, so {@link battleStep} resets
 * it at the start of every call.
 */
export interface BattleStepLog {
	appliedEffects: AppliedEffect[];
	enemyDotDamage: number;
	playerDotDamage: number;
	enemyHotHeal: number;
	playerHotHeal: number;
}

/**
 * Runs one deterministic battle tick — the player's ready skills fire at the
 * enemy, then (only if the enemy survives) the enemy's ready skills fire back —
 * and returns the resulting activations in the order they occurred.
 *
 * This is the single source of the frontend's per-tick battle arithmetic. Both
 * the live {@link BattleEngine} (UI-driven, one call per logical tick) and the
 * headless {@link BattleSimulator} (the parity/test loop) call it, mirroring how
 * the backend's BattleSimulator is the single source its parity test drives.
 * Keeping the exchange here — rather than duplicated in a test — means a change
 * to {@link Battler.advanceCooldowns}, {@link Skill.calculateDamage} or
 * {@link Battler.takeDamage} cannot let the live game diverge from the backend
 * replay while a copied test stays green.
 */
export function battleStep(
	player: Battler,
	enemy: Battler,
	timeDelta: number,
	rng: Mulberry32,
	log?: BattleStepLog
): SkillActivation[] {
	const activations: SkillActivation[] = [];

	// Reset the (reused) observation sink for this tick; absent for the headless simulator.
	if (log) {
		log.appliedEffects.length = 0;
		log.enemyDotDamage = 0;
		log.playerDotDamage = 0;
		log.enemyHotHeal = 0;
		log.playerHotHeal = 0;
	}
	const onApplied = log
		? (effect: ISkillEffect, target: Battler, amount: number) =>
				log.appliedEffects.push({ effect, onPlayer: target === player, amount })
		: undefined;

	// Expire timed effects at the start of the tick, before either side fires, so an effect influences
	// exactly durationMs / tickSize ticks (counting its application tick).
	player.advanceEffects(timeDelta);
	enemy.advanceEffects(timeDelta);

	// Resolve each loadout slot fully in order — accrue, fire, damage, then apply effects — before the
	// next slot accrues, so an earlier slot's effect (e.g. a self CooldownRecovery buff) influences a
	// later slot on the same tick, exactly as the backend's per-slot BattleSkill.Update does. Each fire
	// draws from the shared seeded RNG in a fixed, outcome-independent order (1 crit draw per player fire,
	// then a parry draw + a dodge draw per enemy fire, both unconditional — #1457; Block's second draw was
	// retired, #1330) so both simulators stay in lockstep.
	player.advanceCooldowns(timeDelta, (skill) => {
		// Player crit: one draw (always), independent of portion count — rolled against the firing skill's own
		// base CriticalChance (the per-skill opt-in enabler, #1453) scaled by the player's CriticalChanceMultiplier
		// (base 1, so an unenhanced skill still crits at its own authored rate). A single crit multiplies EVERY
		// portion by CriticalDamage BEFORE mitigation.
		const crit = rng.next() < skill.criticalChance * player.attributes.getValue(EAttribute.CriticalChanceMultiplier);
		const raw = skill.calculateDamage();
		const critMultiplier = crit ? player.attributes.getValue(EAttribute.CriticalDamage) : 1;
		// The Cull execute overlay (#1430): the enemy's missing-health fraction AT THE START of this fire
		// (sampled once, like the crit draw) scales ExecuteBonus into this fire's multiplier above 1, applied
		// identically to critMultiplier before mitigation. Unlike Hex/Momentum/Sunder — backend-only training
		// signals that ride the existing resistance/amplification pipeline — this changes real damage, so it is
		// parity-critical and mirrors the backend BattleContext.DamageTarget.
		const maxHealth = enemy.attributes.getValue(EAttribute.MaxHealth);
		const missingHpFraction = Math.min(1, Math.max(0, (maxHealth - enemy.currentHealth) / maxHealth));
		const executeMultiplier = 1 + player.attributes.getValue(EAttribute.ExecuteBonus) * missingHpFraction;
		// Split the raw hit across the skill's weighted portions, each amplified, crit- and execute-scaled, then
		// run through the typed mitigation pipeline (resistance, Toughness curve scaled by the player's level),
		// summing the nets.
		const damage = dealPortionedDamage(player, enemy, raw, skill.damagePortions, critMultiplier, executeMultiplier);
		// Direct-hit reflection: the enemy (defender) returns its share of the summed net to the player (attacker).
		const reflected = reflectDamage(player, enemy, damage);
		activations.push({ skill, damage, byPlayer: true, crit, dodged: false, parried: false, reflected });
		skill.applyEffects(enemy, onApplied);
	});

	// The enemy fires only while BOTH battlers live: a dead enemy never retaliates (mirroring the backend's
	// victory return after the player's turn), and a player killed by the enemy's reflection during its own
	// attack ends the tick as a loss before the enemy can swing (the backend's player-death check after the
	// player's turn).
	if (!enemy.isDead && !player.isDead) {
		enemy.advanceCooldowns(timeDelta, (skill) => {
			// Two draws in fixed order — parry, then dodge — both unconditional (#1457), so the stream stays in
			// lockstep regardless of portion count or outcome. Parry is checked first so a dodge investment never
			// starves riposte output (the two avoidance layers don't compete for the same draw).
			const parried =
				rng.next() <
				player.attributes.getValue(EAttribute.ParryChance) *
					player.attributes.getValue(EAttribute.ParryChanceMultiplier);
			const dodged = rng.next() < player.attributes.getValue(EAttribute.DodgeChance);
			const raw = skill.calculateDamage();
			// Split the raw hit across the skill's weighted portions: the enemy (attacker) amplifies each, the
			// player resists + Toughness-mitigates each (scaled by the enemy's level), and the nets are summed.
			// A parry or a dodge each zero the whole hit; parry additionally fires the counter (below).
			const damage = parried || dodged ? 0 : dealPortionedDamage(enemy, player, raw, skill.damagePortions, 1);
			// Direct-hit reflection: the player (defender) returns its share of the summed net to the enemy (attacker).
			const reflected = reflectDamage(enemy, player, damage);
			activations.push({ skill, damage, byPlayer: false, crit: false, dodged: !parried && dodged, parried, reflected });
			if (parried) {
				const counter = fireCounter(player, enemy, rng);
				if (counter) {
					activations.push(counter);
				}
			}
			skill.applyEffects(player, onApplied);
		});
	}

	// End-of-tick damage/heal-over-time, reached only while both battlers still live (mirroring the
	// backend's both-alive guard). For each battler the typed DoT accumulators (bypassing the Toughness curve) then
	// HealthRegenPerSecond (capped at MaxHealth) apply before its death check — so a heal-over-time can
	// save a battler from an otherwise-lethal DoT tick. The enemy resolves first: an enemy a same-tick
	// regen cannot save dies before the player's DoT applies, so a same-tick mutual DoT kill leaves the
	// player alive.
	if (!player.isDead && !enemy.isDead) {
		const enemyDot = enemy.applyDamageOverTime(timeDelta);
		const enemyHot = enemy.applyHealOverTime(timeDelta);
		if (log) {
			log.enemyDotDamage = enemyDot;
			log.enemyHotHeal = enemyHot;
		}
		if (!enemy.isDead) {
			const playerDot = player.applyDamageOverTime(timeDelta);
			const playerHot = player.applyHealOverTime(timeDelta);
			if (log) {
				log.playerDotDamage = playerDot;
				log.playerHotHeal = playerHot;
			}
		}
	}

	return activations;
}
