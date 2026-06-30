import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EDamageType, ERarity, EAttribute, EModifierType, ESkillAcquisition, ESkillEffectTarget } from '$lib/api';
import type { ISkill } from '$lib/api';

// A mutable skill registry backing the mocked `staticData.skills`, so the battleStep ordering test can
// resolve a registered skill exactly as the live game does.
const mockSkills: ISkill[] = [];
vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		}
	}
}));

import { Battler, Skill, battleStep } from '$lib/battle';
import { Mulberry32 } from '$lib/engine/mulberry32';
import { battlerFactory, makeSkill, makeEffect } from './battle-sim-test-utils';

/**
 * Unit coverage for the timed skill-effect bookkeeping on {@link Battler}, {@link Skill} and
 * {@link battleStep}. Mirrors the backend suite `Game.Core.Tests/Battle/BattlerEffectsTests.cs` —
 * identical scenarios and expected values on both sides of the battle-parity boundary.
 */

/** Builds a battler from raw stat allocations (no skills) — the frontend analogue of the backend
 *  `MakeBattler(params AttributeModifier[])` helper. */
const makeBattler = (attrs: { id: EAttribute; amount: number }[] = [{ id: EAttribute.Strength, amount: 10 }]) =>
	new Battler({
		name: 'B',
		level: 1,
		selectedSkills: [],
		attributes: attrs.map((a) => ({ attributeId: a.id, amount: a.amount }))
	});

const effect = (
	id: number,
	attribute: EAttribute,
	type: EModifierType,
	amount: number,
	durationMs = 1000,
	target: ESkillEffectTarget = ESkillEffectTarget.Self
) => makeEffect(id, target, attribute, type, amount, durationMs);

describe('Battler skill-effect bookkeeping', () => {
	beforeEach(() => {
		mockSkills.length = 0;
	});

	it('adds a modifier that raises the attribute', () => {
		const battler = makeBattler();

		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5));

		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
	});

	it('stacks when the same effect is applied twice', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5);

		battler.applyEffect(e);
		battler.applyEffect(e);

		// Each application adds its own modifier, so re-applying the same authored effect stacks the
		// magnitude (20 = 10 base + 5 + 5).
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(20);
	});

	it('stacks two different effects on the same attribute', () => {
		const battler = makeBattler();

		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5));
		battler.applyEffect(effect(2, EAttribute.Strength, EModifierType.Additive, 3));

		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(18);
	});

	it.each([
		[40, 1], // durationMs / 40 = 1 tick influenced (the application tick)
		[80, 2],
		[120, 3]
	])('removes an effect after its duration of %i ms (%i ticks)', (durationMs, influencedTicks) => {
		const battler = makeBattler();
		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5, durationMs));
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);

		for (let tick = 1; tick < influencedTicks; tick++) {
			battler.advanceEffects(40);
			expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
		}

		battler.advanceEffects(40);
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(10);
	});

	it('shares a single expiration that resets to the latest application, extending the older one', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5, 80);

		battler.applyEffect(e); // application A: remaining 80
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);

		battler.advanceEffects(40); // A remaining 80 → 40, still active
		battler.applyEffect(e); // application B resets the shared remaining to 80
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(20); // both stacked

		// Independent expirations would drop A here (it had 40 left); the shared reset to 80 extends it.
		battler.advanceEffects(40); // both 80 → 40
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(20);

		battler.advanceEffects(40); // both 40 → 0, the whole stack expires together
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(10);
	});

	it('cuts the stack short when a shorter effect on the same attribute is re-applied', () => {
		const battler = makeBattler();

		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5, 200)); // A: remaining 200
		battler.advanceEffects(40); // A remaining 160
		battler.applyEffect(effect(2, EAttribute.Strength, EModifierType.Additive, 3, 40)); // B resets shared remaining to 40
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(18); // 10 + 5 + 3 (both stacked)

		// The shorter application sets the shared remaining, cutting A short from 160 to 40, so the whole
		// stack expires together on the next tick.
		battler.advanceEffects(40);
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(10);
	});

	it('expires effects on different attributes independently', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.Agility, amount: 10 }
		]);

		// The shared expiry is per-attribute, so a second effect on a DIFFERENT attribute neither stacks
		// with nor resets the first.
		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5, 200)); // Strength remaining 200
		battler.advanceEffects(40); // Strength 160
		battler.applyEffect(effect(2, EAttribute.Agility, EModifierType.Additive, 3, 40)); // Agility remaining 40, leaves Strength alone

		battler.advanceEffects(40); // Agility expires, Strength (still 120) remains
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
		expect(battler.attributes.getValue(EAttribute.Agility)).toBe(10);
	});

	it('raises MaxHealth via the derived cascade without healing', () => {
		const battler = makeBattler(); // MaxHealth = 50 + 5*10 = 100
		expect(battler.attributes.getValue(EAttribute.MaxHealth)).toBe(100);
		expect(battler.currentHealth).toBe(100);

		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 10)); // Str 10 → 20

		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(20);
		expect(battler.attributes.getValue(EAttribute.MaxHealth)).toBe(150); // 50 + 5*20
		expect(battler.currentHealth).toBe(100); // a rise in MaxHealth never heals
	});

	it('clamps current health down when a debuff lowers MaxHealth', () => {
		const battler = makeBattler(); // MaxHealth = currentHealth = 100

		battler.applyEffect(effect(1, EAttribute.MaxHealth, EModifierType.Multiplicative, 0.5));

		expect(battler.attributes.getValue(EAttribute.MaxHealth)).toBe(50);
		expect(battler.currentHealth).toBe(50); // clamped down to the new max
	});

	it('clamps current health to the dropped max when a buff expires', () => {
		const battler = makeBattler(); // base MaxHealth = 50 + 5*10 = 100

		// A Strength buff (1 tick) lifts MaxHealth to (50 + 5*30) = 200 via the derived cascade; a direct
		// ×0.5 MaxHealth (long) halves it back to 100, so currentHealth (100) is not clamped while both are
		// active. They sit on DIFFERENT attributes (Strength vs MaxHealth), so they expire independently.
		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 20, 40));
		battler.applyEffect(effect(2, EAttribute.MaxHealth, EModifierType.Multiplicative, 0.5, 1000));
		expect(battler.attributes.getValue(EAttribute.MaxHealth)).toBe(100);
		expect(battler.currentHealth).toBe(100);

		// The Strength buff expires: MaxHealth drops to (50 + 5*10) * 0.5 = 50, dragging currentHealth down.
		battler.advanceEffects(40);
		expect(battler.attributes.getValue(EAttribute.MaxHealth)).toBe(50);
		expect(battler.currentHealth).toBe(50);
	});

	it('clears active effects on reset so buffs do not carry into the next battle', () => {
		const battler = makeBattler();
		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 10));
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(20);

		battler.reset(); // a data-less reset keeps the attribute set, so the effect modifiers must be removed

		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(10);
		// And no orphaned bookkeeping remains to act on a later expiry tick.
		battler.advanceEffects(40);
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(10);
	});

	it('routes a skill’s Self effect to the caster and Opponent effect to the foe', () => {
		const caster = makeBattler();
		const foe = makeBattler();
		const skill = new Skill(
			{
				id: 0,
				name: 'Dual',
				baseDamage: 0,
				cooldownMs: 40,
				damageMultipliers: [],
				effects: [
					effect(1, EAttribute.Strength, EModifierType.Additive, 5, 1000, ESkillEffectTarget.Self),
					effect(2, EAttribute.Strength, EModifierType.Additive, 7, 1000, ESkillEffectTarget.Opponent)
				],
				description: '',
				iconPath: '',
				rarityId: ERarity.Common,
				word: '',
				pronunciation: '',
				translation: '',
				damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
				acquisition: ESkillAcquisition.Player
			},
			caster
		);

		skill.applyEffects(foe);

		expect(caster.attributes.getValue(EAttribute.Strength)).toBe(15);
		expect(foe.attributes.getValue(EAttribute.Strength)).toBe(17);
	});

	it('reports each newly-applied effect (and the battler it landed on) to the onApplied callback', () => {
		const caster = makeBattler();
		const foe = makeBattler();
		const skill = new Skill(
			{
				id: 0,
				name: 'Dual',
				baseDamage: 0,
				cooldownMs: 40,
				damageMultipliers: [],
				effects: [
					effect(1, EAttribute.Strength, EModifierType.Additive, 5, 1000, ESkillEffectTarget.Self),
					effect(2, EAttribute.Strength, EModifierType.Additive, 7, 1000, ESkillEffectTarget.Opponent)
				],
				description: '',
				iconPath: '',
				rarityId: ERarity.Common,
				word: '',
				pronunciation: '',
				translation: '',
				damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
				acquisition: ESkillAcquisition.Player
			},
			caster
		);

		const applied: { id: number; onCaster: boolean }[] = [];
		skill.applyEffects(foe, (e, target) => applied.push({ id: e.id, onCaster: target === caster }));

		expect(applied).toEqual([
			{ id: 1, onCaster: true },
			{ id: 2, onCaster: false }
		]);

		// Re-firing while both effects are still active now STACKS them, so each application is reported.
		applied.length = 0;
		skill.applyEffects(foe, (e, target) => applied.push({ id: e.id, onCaster: target === caster }));
		expect(applied).toEqual([
			{ id: 1, onCaster: true },
			{ id: 2, onCaster: false }
		]);
	});

	it('exposes a reactive view of active effects for the chips, populated from the authored effect', () => {
		const battler = makeBattler();

		battler.applyEffect(effect(7, EAttribute.Toughness, EModifierType.Additive, 4, 1000));

		expect(battler.activeEffects).toHaveLength(1);
		expect(battler.activeEffects[0]).toEqual({
			attribute: EAttribute.Toughness,
			modifierType: EModifierType.Additive,
			totalAmount: 4,
			count: 1,
			durationMs: 1000,
			remainingMs: 1000,
			renderRemainingMs: 1000,
			sources: [{ sourceId: 7, amount: 4, count: 1 }]
		});
	});

	it('folds a repeat application into the one view (stacking), resetting the shared remaining', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5, 200);

		battler.applyEffect(e);
		battler.advanceEffects(40); // remaining 200 → 160
		expect(battler.activeEffects[0].remainingMs).toBe(160);

		battler.applyEffect(e); // folds a second application into the same view and resets the shared remaining
		expect(battler.activeEffects).toHaveLength(1);
		expect(battler.activeEffects[0].count).toBe(2);
		expect(battler.activeEffects[0].totalAmount).toBe(10); // 5 + 5
		expect(battler.activeEffects[0].remainingMs).toBe(200);
		expect(battler.activeEffects[0].renderRemainingMs).toBe(200);
		// The two applications come from one source, so the per-source breakdown aggregates rather than grows.
		expect(battler.activeEffects[0].sources).toEqual([{ sourceId: 1, amount: 10, count: 2 }]);
	});

	it('folds different source effects on one attribute+type into one view with a per-source breakdown', () => {
		const battler = makeBattler();

		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5));
		battler.applyEffect(effect(2, EAttribute.Strength, EModifierType.Additive, 3));

		expect(battler.activeEffects).toHaveLength(1);
		expect(battler.activeEffects[0].count).toBe(2);
		expect(battler.activeEffects[0].totalAmount).toBe(8); // 5 + 3
		expect(battler.activeEffects[0].sources).toEqual([
			{ sourceId: 1, amount: 5, count: 1 },
			{ sourceId: 2, amount: 3, count: 1 }
		]);
	});

	it('drops the view when its effect expires and clears all views on reset', () => {
		const battler = makeBattler();
		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5, 40));
		expect(battler.activeEffects).toHaveLength(1);

		battler.advanceEffects(40); // expires
		expect(battler.activeEffects).toHaveLength(0);

		battler.applyEffect(effect(2, EAttribute.Strength, EModifierType.Additive, 5, 1000));
		battler.reset();
		expect(battler.activeEffects).toHaveLength(0);
	});

	it('interpolates renderRemainingMs toward the next tick, clamped at zero', () => {
		const battler = makeBattler();
		battler.applyEffect(effect(1, EAttribute.Strength, EModifierType.Additive, 5, 1000));
		battler.advanceEffects(40); // remainingMs 1000 → 960; renderRemainingMs untouched (still 1000)

		battler.updateRenderEffects(10);
		expect(battler.activeEffects[0].renderRemainingMs).toBe(950); // 960 - 10

		// Render delta past the logical remaining clamps to 0 rather than going negative.
		battler.updateRenderEffects(2000);
		expect(battler.activeEffects[0].renderRemainingMs).toBe(0);
	});

	it('deals a skill’s damage before applying its self buff', () => {
		// The shared battleStep is the single per-tick path; firing a Strength-scaling skill that also
		// self-buffs Strength must hit for the pre-buff value, then leave the caster buffed for later hits.
		const make = battlerFactory(mockSkills);
		const player = make(
			[{ id: EAttribute.Strength, amount: 10 }],
			[
				makeSkill(
					0,
					40,
					[{ attributeId: EAttribute.Strength, multiplier: 1.0 }],
					[makeEffect(1, ESkillEffectTarget.Self, EAttribute.Strength, EModifierType.Additive, 10, 1000)]
				)
			]
		);
		const enemy = make([{ id: EAttribute.Strength, amount: 10 }], []); // no Endurance → Toughness 0

		battleStep(player, enemy, 40, new Mulberry32(0)); // charges 40 ≥ 40 cooldown → fires this tick

		// The carrying hit used pre-buff Strength (10): 10 dealt (the enemy has no Toughness).
		expect(enemy.currentHealth).toBe(90);
		// The self buff lands only after the hit, so the caster is now boosted for later hits.
		expect(player.attributes.getValue(EAttribute.Strength)).toBe(20);
	});
});

/**
 * Caster-attribute scaling of a skill effect's magnitude (#741). Mirrors the backend suite's
 * `BattleContext_ApplySkillEffect_*` scaling tests — the scaling happens at the fire site
 * ({@link Skill.applyEffects}, the analogue of the backend `BattleContext.ApplySkillEffect`).
 */
describe('Skill effect attribute scaling', () => {
	const skillWith = (effect: ReturnType<typeof makeEffect>, caster: Battler) =>
		new Skill(
			{
				id: 0,
				name: 'Scaler',
				baseDamage: 0,
				cooldownMs: 40,
				damageMultipliers: [],
				effects: [effect],
				description: '',
				iconPath: '',
				rarityId: ERarity.Common,
				word: '',
				pronunciation: '',
				translation: '',
				damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
				acquisition: ESkillAcquisition.Player
			},
			caster
		);

	const poison = (baseAmount: number, scalingAmount: number) =>
		makeEffect(
			1,
			ESkillEffectTarget.Opponent,
			EAttribute.BleedDamagePerSecond,
			EModifierType.Additive,
			baseAmount,
			1000,
			EAttribute.Dexterity,
			scalingAmount
		);

	it('scales the effect magnitude with the caster attribute', () => {
		const caster = makeBattler([{ id: EAttribute.Dexterity, amount: 20 }]);
		const foe = makeBattler([{ id: EAttribute.Dexterity, amount: 0 }]);

		skillWith(poison(10, 0.5), caster).applyEffects(foe);

		// 10 + caster Dexterity(20) × 0.5 = 20
		expect(foe.attributes.getValue(EAttribute.BleedDamagePerSecond)).toBe(20);
	});

	it('reads the caster attribute, not the target', () => {
		const caster = makeBattler([{ id: EAttribute.Dexterity, amount: 4 }]);
		const foe = makeBattler([{ id: EAttribute.Dexterity, amount: 100 }]);

		skillWith(poison(0, 1.0), caster).applyEffects(foe);

		// 0 + caster Dexterity(4) × 1.0 = 4, not the target's 100.
		expect(foe.attributes.getValue(EAttribute.BleedDamagePerSecond)).toBe(4);
	});

	it('leaves the amount unchanged when scalingAmount is 0', () => {
		const caster = makeBattler([{ id: EAttribute.Dexterity, amount: 50 }]);
		const foe = makeBattler([{ id: EAttribute.Dexterity, amount: 0 }]);

		skillWith(poison(7, 0), caster).applyEffects(foe);

		expect(foe.attributes.getValue(EAttribute.BleedDamagePerSecond)).toBe(7);
	});

	it('records the scaled amount on the active-effect view and reports it to onApplied', () => {
		const caster = makeBattler([{ id: EAttribute.Dexterity, amount: 20 }]);
		const foe = makeBattler([{ id: EAttribute.Dexterity, amount: 0 }]);

		const applied: number[] = [];
		skillWith(poison(10, 0.5), caster).applyEffects(foe, (_effect, _target, amount) => applied.push(amount));

		// The chip view and the log both surface the scaled magnitude, not the authored base.
		expect(foe.activeEffects[0].totalAmount).toBe(20);
		expect(applied).toEqual([20]);
	});

	// Caster amplification freeze (#1320 Area C). Mirrors the backend
	// `BattleContext_ApplySkillEffect_Freezes…` / `…Amplification…` tests.

	it('freezes caster amplification into a DoT accumulator at apply time', () => {
		const caster = makeBattler([{ id: EAttribute.BleedAmplification, amount: 0.5 }]);
		const foe = makeBattler([{ id: EAttribute.Strength, amount: 0 }]);

		const effect = makeEffect(
			1,
			ESkillEffectTarget.Opponent,
			EAttribute.BleedDamagePerSecond,
			EModifierType.Additive,
			10,
			1000
		);
		skillWith(effect, caster).applyEffects(foe);

		// base 10 × (1 + 0.5 BleedAmplification) = 15
		expect(foe.attributes.getValue(EAttribute.BleedDamagePerSecond)).toBe(15);
	});

	it('applies the amplification freeze after caster scaling', () => {
		const caster = makeBattler([
			{ id: EAttribute.Intellect, amount: 20 },
			{ id: EAttribute.DotAmplification, amount: 0.5 }
		]);
		const foe = makeBattler([{ id: EAttribute.Strength, amount: 0 }]);

		const effect = makeEffect(
			1,
			ESkillEffectTarget.Opponent,
			EAttribute.PoisonDamagePerSecond,
			EModifierType.Additive,
			0,
			1000,
			EAttribute.Intellect,
			0.5
		);
		skillWith(effect, caster).applyEffects(foe);

		// base 0 + Intellect(20) × 0.5 = 10, × (1 + 0.5 DotAmplification) = 15
		expect(foe.attributes.getValue(EAttribute.PoisonDamagePerSecond)).toBe(15);
	});

	it('amplifies burn through the cross-cutting fire key', () => {
		const caster = makeBattler([{ id: EAttribute.FireAmplification, amount: 1.0 }]);
		const foe = makeBattler([{ id: EAttribute.Strength, amount: 0 }]);

		const effect = makeEffect(
			1,
			ESkillEffectTarget.Opponent,
			EAttribute.BurnDamagePerSecond,
			EModifierType.Additive,
			10,
			1000
		);
		skillWith(effect, caster).applyEffects(foe);

		// base 10 × (1 + 1.0 FireAmplification) = 20
		expect(foe.attributes.getValue(EAttribute.BurnDamagePerSecond)).toBe(20);
	});

	it('freezes the caster amplification, not the target', () => {
		const caster = makeBattler([{ id: EAttribute.Strength, amount: 0 }]);
		const foe = makeBattler([{ id: EAttribute.BleedAmplification, amount: 5.0 }]);

		const effect = makeEffect(
			1,
			ESkillEffectTarget.Opponent,
			EAttribute.BleedDamagePerSecond,
			EModifierType.Additive,
			10,
			1000
		);
		skillWith(effect, caster).applyEffects(foe);

		// The target's amplification is irrelevant: base 10 stays 10.
		expect(foe.attributes.getValue(EAttribute.BleedDamagePerSecond)).toBe(10);
	});

	it('leaves the typeless HoT channel unamplified', () => {
		const caster = makeBattler([{ id: EAttribute.DotAmplification, amount: 1.0 }]);
		const foe = makeBattler([{ id: EAttribute.Strength, amount: 0 }]);

		const effect = makeEffect(
			1,
			ESkillEffectTarget.Self,
			EAttribute.HealthRegenPerSecond,
			EModifierType.Additive,
			12,
			1000
		);
		skillWith(effect, caster).applyEffects(foe);

		expect(caster.attributes.getValue(EAttribute.HealthRegenPerSecond)).toBe(12);
	});
});
