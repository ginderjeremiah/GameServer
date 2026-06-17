import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EModifierType, ESkillEffectTarget } from '$lib/api';
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

	it('refreshes without stacking when the same effect is applied twice', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5);

		battler.applyEffect(e);
		battler.applyEffect(e);

		// A second application of the same authored effect refreshes its duration rather than adding a
		// second modifier, so the magnitude does not stack (15, not 20).
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
	});

	it('returns true for a new application and false for a refresh', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5);

		expect(battler.applyEffect(e)).toBe(true);
		expect(battler.applyEffect(e)).toBe(false);
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

	it('refresh resets the remaining duration', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5, 80);

		battler.applyEffect(e);
		battler.advanceEffects(40); // remaining 80 → 40
		battler.applyEffect(e); // refresh remaining back to 80

		battler.advanceEffects(40); // 80 → 40, still active
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(15);
		battler.advanceEffects(40); // 40 → 0, removed
		expect(battler.attributes.getValue(EAttribute.Strength)).toBe(10);
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
		const battler = makeBattler(); // base MaxHealth = 100

		// H (additive, 1 tick) lifts the additive subtotal to 200; L (×0.5, long) halves it back to 100,
		// so currentHealth (100) is not clamped while both are active.
		battler.applyEffect(effect(1, EAttribute.MaxHealth, EModifierType.Additive, 100, 40));
		battler.applyEffect(effect(2, EAttribute.MaxHealth, EModifierType.Multiplicative, 0.5, 1000));
		expect(battler.attributes.getValue(EAttribute.MaxHealth)).toBe(100);
		expect(battler.currentHealth).toBe(100);

		// H expires: MaxHealth drops to 100 * 0.5 = 50, dragging currentHealth down with it.
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
				iconPath: ''
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
				iconPath: ''
			},
			caster
		);

		const applied: { id: number; onCaster: boolean }[] = [];
		skill.applyEffects(foe, (e, target) => applied.push({ id: e.id, onCaster: target === caster }));

		expect(applied).toEqual([
			{ id: 1, onCaster: true },
			{ id: 2, onCaster: false }
		]);

		// Re-firing while both effects are still active refreshes them, so nothing new is reported.
		applied.length = 0;
		skill.applyEffects(foe, (e, target) => applied.push({ id: e.id, onCaster: target === caster }));
		expect(applied).toHaveLength(0);
	});

	it('exposes a reactive view of active effects for the chips, populated from the authored effect', () => {
		const battler = makeBattler();

		battler.applyEffect(effect(7, EAttribute.Defense, EModifierType.Additive, 4, 1000));

		expect(battler.activeEffects).toHaveLength(1);
		expect(battler.activeEffects[0]).toEqual({
			sourceId: 7,
			attribute: EAttribute.Defense,
			modifierType: EModifierType.Additive,
			amount: 4,
			durationMs: 1000,
			remainingMs: 1000,
			renderRemainingMs: 1000
		});
	});

	it('refresh resets the view’s remaining (logical and render) without adding a second view', () => {
		const battler = makeBattler();
		const e = effect(1, EAttribute.Strength, EModifierType.Additive, 5, 200);

		battler.applyEffect(e);
		battler.advanceEffects(40); // remaining 200 → 160
		expect(battler.activeEffects[0].remainingMs).toBe(160);

		battler.applyEffect(e); // refresh
		expect(battler.activeEffects).toHaveLength(1);
		expect(battler.activeEffects[0].remainingMs).toBe(200);
		expect(battler.activeEffects[0].renderRemainingMs).toBe(200);
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
		const enemy = make([{ id: EAttribute.Strength, amount: 10 }], []); // Defense = 2

		battleStep(player, enemy, 40, new Mulberry32(0)); // charges 40 ≥ 40 cooldown → fires this tick

		// The carrying hit used pre-buff Strength (10): 10 - 2 defense = 8 dealt.
		expect(enemy.currentHealth).toBe(92);
		// The self buff lands only after the hit, so the caster is now boosted for later hits.
		expect(player.attributes.getValue(EAttribute.Strength)).toBe(20);
	});
});
