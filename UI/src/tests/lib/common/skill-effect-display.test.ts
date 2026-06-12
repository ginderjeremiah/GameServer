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
	it('is a buff when a beneficial attribute is raised and a debuff when lowered', () => {
		expect(effectDirection(EAttribute.Strength, EModifierType.Additive, 5)).toBe('buff');
		expect(effectDirection(EAttribute.Strength, EModifierType.Additive, -5)).toBe('debuff');
		expect(effectDirection(EAttribute.MaxHealth, EModifierType.Multiplicative, 1.5)).toBe('buff');
		expect(effectDirection(EAttribute.MaxHealth, EModifierType.Multiplicative, 0.5)).toBe('debuff');
	});

	it('inverts for a harmful-when-raised attribute (DamageTakenPerSecond)', () => {
		// Raising incoming damage is a debuff; lowering it is a buff — the opposite of a normal attribute.
		expect(effectDirection(EAttribute.DamageTakenPerSecond, EModifierType.Additive, 12)).toBe('debuff');
		expect(effectDirection(EAttribute.DamageTakenPerSecond, EModifierType.Additive, -12)).toBe('buff');
	});

	it('treats HealthRegenPerSecond as beneficial when raised', () => {
		expect(effectDirection(EAttribute.HealthRegenPerSecond, EModifierType.Additive, 8)).toBe('buff');
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
			'Strength'
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
			'Defense'
		);

		expect(description.direction).toBe('debuff');
		expect(description.text).toBe('-10 Defense (enemy), 3s');
	});
});

describe('effectLogMessage', () => {
	it('phrases a buff landing on the player as "You are empowered"', () => {
		const message = effectLogMessage(
			effect({ attributeId: EAttribute.Strength, amount: 15, durationMs: 5000 }),
			'Strength',
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
			'Goblin'
		);
		expect(message).toBe('Goblin is weakened: -10 Defense for 3s');
	});

	it('classifies the direction by what the effect does to its target, not the magnitude sign', () => {
		// A positive DamageTakenPerSecond is detrimental, so applying it to the enemy is a weakening debuff.
		const message = effectLogMessage(
			effect({ attributeId: EAttribute.DamageTakenPerSecond, amount: 12, durationMs: 3000 }),
			'Damage Taken Per Second',
			false,
			'Goblin'
		);
		expect(message).toBe('Goblin is weakened: +12 Damage Taken Per Second for 3s');
	});
});
