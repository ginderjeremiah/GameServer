import { describe, it, expect } from 'vitest';
import { EAttribute, EModifierType, ESkillEffectTarget, type ISkillEffect } from '$lib/api';
import {
	describeEffect,
	effectDirection,
	effectDirectionColor,
	effectLogMessage,
	effectRaisesAttribute,
	effectTargetLabel,
	formatEffectDuration,
	formatEffectMagnitude
} from '$lib/common';

const effect = (over: Partial<ISkillEffect> = {}): ISkillEffect => ({
	id: 1,
	target: ESkillEffectTarget.Self,
	attributeId: EAttribute.Strength,
	modifierTypeId: EModifierType.Additive,
	amount: 15,
	durationMs: 5000,
	scalingAttributeId: EAttribute.Strength,
	scalingAmount: 0,
	...over
});

describe('effectRaisesAttribute', () => {
	it('treats a positive additive amount as raising and a negative one as lowering', () => {
		expect(effectRaisesAttribute(EModifierType.Additive, 5)).toBe(true);
		expect(effectRaisesAttribute(EModifierType.Additive, -5)).toBe(false);
	});

	it('treats a multiplicative factor above 1 as raising and below 1 as lowering', () => {
		expect(effectRaisesAttribute(EModifierType.Multiplicative, 1.5)).toBe(true);
		expect(effectRaisesAttribute(EModifierType.Multiplicative, 0.5)).toBe(false);
	});
});

describe('effectDirection', () => {
	it('is a buff when a beneficial (non-harmful) attribute is raised and a debuff when lowered', () => {
		expect(effectDirection(false, EModifierType.Additive, 5)).toBe('buff');
		expect(effectDirection(false, EModifierType.Additive, -5)).toBe('debuff');
		expect(effectDirection(false, EModifierType.Multiplicative, 1.5)).toBe('buff');
		expect(effectDirection(false, EModifierType.Multiplicative, 0.5)).toBe('debuff');
	});

	it('inverts for a harmful-when-raised attribute (e.g. BleedDamagePerSecond)', () => {
		// Raising incoming damage is a debuff; lowering it is a buff — the opposite of a normal attribute.
		expect(effectDirection(true, EModifierType.Additive, 12)).toBe('debuff');
		expect(effectDirection(true, EModifierType.Additive, -12)).toBe('buff');
	});
});

describe('formatEffectMagnitude', () => {
	it('signs additive amounts and prefixes multiplicative ones with ×', () => {
		expect(formatEffectMagnitude(EModifierType.Additive, 15)).toBe('+15');
		expect(formatEffectMagnitude(EModifierType.Additive, -15)).toBe('-15');
		expect(formatEffectMagnitude(EModifierType.Multiplicative, 0.5)).toBe('×0.5');
		expect(formatEffectMagnitude(EModifierType.Multiplicative, 1.5)).toBe('×1.5');
	});
});

describe('formatEffectDuration', () => {
	it('renders milliseconds as seconds', () => {
		expect(formatEffectDuration(5000)).toBe('5s');
		expect(formatEffectDuration(2500)).toBe('2.5s');
		expect(formatEffectDuration(40)).toBe('0.04s');
	});
});

describe('effectTargetLabel', () => {
	it('maps Self to "self" and Opponent to "enemy"', () => {
		expect(effectTargetLabel(ESkillEffectTarget.Self)).toBe('self');
		expect(effectTargetLabel(ESkillEffectTarget.Opponent)).toBe('enemy');
	});
});

describe('effectDirectionColor', () => {
	it('returns the themeable buff/debuff vars', () => {
		expect(effectDirectionColor('buff')).toBe('var(--effect-buff)');
		expect(effectDirectionColor('debuff')).toBe('var(--effect-debuff)');
	});
});

describe('describeEffect', () => {
	it('assembles the structured pieces and one-line summary', () => {
		const description = describeEffect(
			effect({ attributeId: EAttribute.Strength, amount: 15, durationMs: 5000, target: ESkillEffectTarget.Self }),
			'Strength',
			false
		);

		expect(description).toEqual({
			direction: 'buff',
			magnitude: '+15',
			attributeName: 'Strength',
			targetLabel: 'self',
			duration: '5s',
			text: '+15 Strength (self), 5s'
		});
	});

	it('describes an opponent debuff', () => {
		const description = describeEffect(
			effect({
				target: ESkillEffectTarget.Opponent,
				attributeId: EAttribute.Defense,
				modifierTypeId: EModifierType.Additive,
				amount: -10,
				durationMs: 3000
			}),
			'Defense',
			false
		);

		expect(description.direction).toBe('debuff');
		expect(description.text).toBe('-10 Defense (enemy), 3s');
	});

	it('uses the provided (caster-scaled) amount for the magnitude and direction', () => {
		// The authored base is 5, but a resolved/scaled amount of 23 is what should render.
		const description = describeEffect(
			effect({ attributeId: EAttribute.Strength, amount: 5, durationMs: 5000, target: ESkillEffectTarget.Self }),
			'Strength',
			false,
			23
		);

		expect(description.magnitude).toBe('+23');
		expect(description.text).toBe('+23 Strength (self), 5s');
	});
});

describe('effectLogMessage', () => {
	it('phrases a buff landing on the player as "You are empowered"', () => {
		const message = effectLogMessage(
			effect({ attributeId: EAttribute.Strength, amount: 15, durationMs: 5000 }),
			'Strength',
			false,
			true,
			'Goblin'
		);
		expect(message).toBe('You are empowered: +15 Strength for 5s');
	});

	it('phrases a debuff landing on the enemy as "<name> is weakened"', () => {
		const message = effectLogMessage(
			effect({
				attributeId: EAttribute.Defense,
				modifierTypeId: EModifierType.Additive,
				amount: -10,
				durationMs: 3000
			}),
			'Defense',
			false,
			false,
			'Goblin'
		);
		expect(message).toBe('Goblin is weakened: -10 Defense for 3s');
	});

	it('classifies the direction by what the effect does to its target, not the magnitude sign', () => {
		// A positive BleedDamagePerSecond is detrimental (isHarmful), so applying it to the enemy is a weakening debuff.
		const message = effectLogMessage(
			effect({ attributeId: EAttribute.BleedDamagePerSecond, amount: 12, durationMs: 3000 }),
			'Bleed Damage Per Second',
			true,
			false,
			'Goblin'
		);
		expect(message).toBe('Goblin is weakened: +12 Bleed Damage Per Second for 3s');
	});

	it('reports the caster-scaled amount when one is supplied', () => {
		// Authored base 12, but the resolved (scaled) magnitude of 20 is what the log should announce.
		const message = effectLogMessage(
			effect({ attributeId: EAttribute.BleedDamagePerSecond, amount: 12, durationMs: 3000 }),
			'Bleed Damage Per Second',
			true,
			false,
			'Goblin',
			20
		);
		expect(message).toBe('Goblin is weakened: +20 Bleed Damage Per Second for 3s');
	});
});
