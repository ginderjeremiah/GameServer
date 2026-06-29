import { describe, it, expect } from 'vitest';
import { EDamageType } from '$lib/api';
import { classifyResist, damageLogMessage } from '../../lib/common/damage-log';

describe('classifyResist', () => {
	it('is normal with no typed resistance in play', () => {
		expect(classifyResist(0, 42)).toBe('normal');
	});

	it('is resisted for positive resistance that still dealt damage', () => {
		expect(classifyResist(0.3, 28)).toBe('resisted');
	});

	it('is resisted when full resistance negates the hit to zero (no heal room)', () => {
		// Resistance ≥ 1 mitigates to ≤ 0, but with no heal room takeDamage returns 0, not a heal.
		expect(classifyResist(1, 0)).toBe('resisted');
	});

	it('is vulnerable for negative resistance (the hit was amplified)', () => {
		expect(classifyResist(-0.5, 63)).toBe('vulnerable');
	});

	it('is absorbed whenever the net damage came back negative (a heal)', () => {
		expect(classifyResist(1.4, -18)).toBe('absorbed');
	});
});

describe('damageLogMessage', () => {
	it('names the damage type for a normal player hit', () => {
		expect(damageLogMessage('Fireball', 45, 'player-hit', EDamageType.Fire, 'normal', 'Goblin')).toBe(
			'You used Fireball and dealt 45 fire damage!'
		);
	});

	it('omits the type word for a physical hit (the untyped baseline)', () => {
		expect(damageLogMessage('Slash', 12, 'player-hit', EDamageType.Physical, 'normal', 'Goblin')).toBe(
			'You used Slash and dealt 12 damage!'
		);
	});

	it('appends a resisted note', () => {
		expect(damageLogMessage('Fireball', 30, 'player-hit', EDamageType.Fire, 'resisted', 'Goblin')).toBe(
			'You used Fireball and dealt 30 fire damage — resisted.'
		);
	});

	it('appends a vulnerable note', () => {
		expect(damageLogMessage('Fireball', 60, 'player-hit', EDamageType.Fire, 'vulnerable', 'Goblin')).toBe(
			'You used Fireball and dealt 60 fire damage — vulnerable!'
		);
	});

	it('phrases a crit with its type', () => {
		expect(damageLogMessage('Fireball', 90, 'player-crit', EDamageType.Fire, 'normal', 'Goblin')).toBe(
			'You landed a critical hit with Fireball for 90 fire damage!'
		);
	});

	it('phrases an incoming enemy hit', () => {
		expect(damageLogMessage('Ember', 8, 'enemy-hit', EDamageType.Fire, 'normal', 'Goblin')).toBe(
			'Goblin used Ember and dealt 8 fire damage!'
		);
	});

	it('phrases a block, carrying the type and a resist note', () => {
		expect(damageLogMessage('Ember', 4, 'player-block', EDamageType.Fire, 'resisted', 'Goblin')).toBe(
			"You blocked Goblin's Ember, taking only 4 fire damage — resisted."
		);
	});

	it('phrases a dodge with no number, type, or resist note', () => {
		expect(damageLogMessage('Ember', 0, 'player-dodge', EDamageType.Fire, 'normal', 'Goblin')).toBe(
			"You dodged Goblin's Ember!"
		);
	});

	it('reports an absorbed player hit as the enemy recovering health', () => {
		// Absorption returns a negative net (the heal), reported as the magnitude recovered.
		expect(damageLogMessage('Fireball', -20, 'player-hit', EDamageType.Fire, 'absorbed', 'Goblin')).toBe(
			'Goblin absorbed your Fireball, recovering 20 health!'
		);
	});

	it('reports an absorbed enemy hit as the player recovering health', () => {
		expect(damageLogMessage('Ember', -6, 'enemy-hit', EDamageType.Fire, 'absorbed', 'Goblin')).toBe(
			"You absorbed Goblin's Ember, recovering 6 health!"
		);
	});
});
