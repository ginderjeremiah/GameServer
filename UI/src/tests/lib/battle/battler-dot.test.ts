import { describe, it, expect, vi } from 'vitest';
import { EAttribute } from '$lib/api';

// The Battler constructor resolves selectedSkills through staticData; an empty registry suffices here.
vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return [];
		}
	}
}));

import { Battler } from '$lib/battle';

/**
 * Unit coverage for the per-tick damage/heal-over-time application on {@link Battler}. Mirrors the
 * per-battler arithmetic in the backend suite `Game.Core.Tests/Battle/BattleDamageOverTimeTests.cs`
 * (the statistics half of that suite is backend-only — the frontend tracks no battle statistics).
 */
const makeBattler = (attrs: { id: EAttribute; amount: number }[]) =>
	new Battler({
		name: 'B',
		level: 1,
		selectedSkills: [],
		attributes: attrs.map((a) => ({ attributeId: a.id, amount: a.amount }))
	});

describe('Battler damage/heal-over-time', () => {
	it('scales DamageTakenPerSecond by the tick and bypasses Defense', () => {
		// Endurance 50 → Defense 52, but damage-over-time ignores Defense entirely.
		const battler = makeBattler([
			{ id: EAttribute.Endurance, amount: 50 },
			{ id: EAttribute.DamageTakenPerSecond, amount: 50 }
		]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40); // 50 * 40 / 1000 = 2

		expect(dealt).toBe(2);
		expect(battler.currentHealth).toBe(startHealth - 2);
	});

	it('floors a negative DamageTakenPerSecond at zero (no heal past the cap)', () => {
		// A negative DamageTakenPerSecond must NOT heal: DoT floors at zero (like takeDamage) so it can't
		// bypass the MaxHealth cap. The battler starts below full to prove no heal.
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 }, // MaxHealth 100
			{ id: EAttribute.DamageTakenPerSecond, amount: -50 }
		]);
		battler.takeDamage(52); // Defense 2 → 50 damage → currentHealth 50

		const dealt = battler.applyDamageOverTime(40); // -50 * 40 / 1000 = -2, floored to 0

		expect(dealt).toBe(0);
		expect(battler.currentHealth).toBe(50);
	});

	it('scales HealthRegenPerSecond by the tick', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 }, // MaxHealth 100
			{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
		]);
		battler.takeDamage(52); // Defense 2 → 50 damage → currentHealth 50

		const healed = battler.applyHealOverTime(40); // 75 * 40 / 1000 = 3

		expect(healed).toBe(3);
		expect(battler.currentHealth).toBe(53);
	});

	it('heals nothing at full health', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
		]);

		const healed = battler.applyHealOverTime(40);

		expect(healed).toBe(0);
		expect(battler.currentHealth).toBe(100);
	});

	it('heals only up to the cap when near MaxHealth', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 }, // MaxHealth 100
			{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
		]);
		battler.takeDamage(4); // Defense 2 → 2 damage → currentHealth 98

		const healed = battler.applyHealOverTime(40); // would heal 3, but only 2 of room remains

		expect(healed).toBe(2);
		expect(battler.currentHealth).toBe(100);
	});

	it('keeps isDead in sync after a heal (matches the backend always-live IsDead)', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 }, // MaxHealth 100
			{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
		]);
		battler.takeDamage(52); // currentHealth 50

		battler.applyHealOverTime(40); // heals 3 → currentHealth 53

		expect(battler.isDead).toBe(battler.currentHealth <= 0);
		expect(battler.isDead).toBe(false);
	});
});
