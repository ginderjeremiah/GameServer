import { describe, it, expect } from 'vitest';
import { EActivityKey } from '$lib/api';
import { activityKeyDisplay, activityKeyLabel } from '$lib/common';

describe('activityKeyLabel', () => {
	it('labels offense keys by their damage-type stem (DoT spelled out)', () => {
		expect(activityKeyLabel(EActivityKey.Fire)).toBe('Fire');
		expect(activityKeyLabel(EActivityKey.Physical)).toBe('Physical');
		expect(activityKeyLabel(EActivityKey.Dot)).toBe('DoT');
	});

	it('labels weapon-mastery leaves by their bare stem', () => {
		expect(activityKeyLabel(EActivityKey.Sword)).toBe('Sword');
		expect(activityKeyLabel(EActivityKey.Unarmed)).toBe('Unarmed');
	});

	it('labels combat events by the quantity they train on', () => {
		expect(activityKeyLabel(EActivityKey.Crit)).toBe('Critical damage');
		expect(activityKeyLabel(EActivityKey.Dodge)).toBe('Dodged damage');
		expect(activityKeyLabel(EActivityKey.Heal)).toBe('Healing done');
		expect(activityKeyLabel(EActivityKey.Reflect)).toBe('Reflected damage');
		expect(activityKeyLabel(EActivityKey.Hex)).toBe('Vulnerability damage enabled');
		expect(activityKeyLabel(EActivityKey.Momentum)).toBe('Ramp damage enabled');
		expect(activityKeyLabel(EActivityKey.Sunder)).toBe('Mitigation damage enabled');
		expect(activityKeyLabel(EActivityKey.Cull)).toBe('Execute damage enabled');
		expect(activityKeyLabel(EActivityKey.Parry)).toBe('Counter damage');
		expect(activityKeyLabel(EActivityKey.Cadence)).toBe('Cadence damage enabled');
	});

	it('labels resist keys with a (resist) suffix on the spelled stem', () => {
		expect(activityKeyLabel(EActivityKey.FireResist)).toBe('Fire (resist)');
		expect(activityKeyLabel(EActivityKey.DotResist)).toBe('DoT (resist)');
	});
});

describe('activityKeyDisplay', () => {
	it('classifies keys into offense / event / resist with the bare label', () => {
		expect(activityKeyDisplay(EActivityKey.Fire)).toEqual({ kind: 'offense', label: 'Fire' });
		expect(activityKeyDisplay(EActivityKey.Crit)).toEqual({ kind: 'event', label: 'Critical damage' });
		// Parry is a combat-event key (counter damage dealt, #1457), not a damage-type stem.
		expect(activityKeyDisplay(EActivityKey.Parry)).toEqual({ kind: 'event', label: 'Counter damage' });
		expect(activityKeyDisplay(EActivityKey.FireResist)).toEqual({ kind: 'resist', label: 'Fire' });
		expect(activityKeyDisplay(EActivityKey.DotResist)).toEqual({ kind: 'resist', label: 'DoT' });
	});

	it('gives every enum key a classification and a non-empty, non-raw-suffixed label', () => {
		const keys = Object.values(EActivityKey).filter((v): v is EActivityKey => typeof v === 'number');
		for (const key of keys) {
			const { kind, label } = activityKeyDisplay(key);
			expect(['offense', 'event', 'resist']).toContain(kind);
			expect(label.length).toBeGreaterThan(0);
			expect(label.endsWith('Resist')).toBe(false);
		}
	});
});
