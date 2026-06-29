import { describe, it, expect, vi } from 'vitest';
import { EDamageType, EAttribute } from '$lib/api';

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
	it('scales a typed accumulator by the tick and bypasses Defense', () => {
		// Endurance 50 → Defense 52, but damage-over-time ignores Defense entirely.
		const battler = makeBattler([
			{ id: EAttribute.Endurance, amount: 50 },
			{ id: EAttribute.BleedDamagePerSecond, amount: 50 }
		]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40); // 50 * 40 / 1000 = 2

		expect(dealt).toBe(2);
		expect(battler.currentHealth).toBe(startHealth - 2);
	});

	it('deals zero with no DoT authored', () => {
		const battler = makeBattler([{ id: EAttribute.Strength, amount: 10 }]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(0);
		expect(battler.currentHealth).toBe(startHealth);
	});

	it('applies the type resistance sampled live', () => {
		// 50 BleedDamagePerSecond → 2/tick, halved by 0.5 BleedResistance → 1.
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.BleedDamagePerSecond, amount: 50 },
			{ id: EAttribute.BleedResistance, amount: 0.5 }
		]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(1);
		expect(battler.currentHealth).toBe(startHealth - 1);
	});

	it('amplifies as vulnerability when resistance is negative', () => {
		// A −1.0 BleedResistance doubles the incoming bleed tick (factor 1 − (−1) = 2): 2 → 4. Unclamped.
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.BleedDamagePerSecond, amount: 50 },
			{ id: EAttribute.BleedResistance, amount: -1.0 }
		]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(4);
		expect(battler.currentHealth).toBe(startHealth - 4);
	});

	it('heals as absorption when resistance is above one', () => {
		// A +2.0 BleedResistance drives the tick negative (2 × (1 − 2) = −2): absorption heals. DoT bypasses
		// Defense and is not floored, so the negative tick restores health (here the battler is below max).
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.BleedDamagePerSecond, amount: 50 },
			{ id: EAttribute.BleedResistance, amount: 2.0 }
		]);
		battler.takeDamage(52, EDamageType.Physical); // Defense 2 → 50 damage → currentHealth 50

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(-2);
		expect(battler.currentHealth).toBe(52); // 50 − (−2)
	});

	it('resists burn through the cross-cutting fire key', () => {
		// Burn resists as burn + fire + elemental + dot: 250 BurnDamagePerSecond → 10/tick, halved by 0.5
		// FireResistance → 5.
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.BurnDamagePerSecond, amount: 250 },
			{ id: EAttribute.FireResistance, amount: 0.5 }
		]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(5);
		expect(battler.currentHealth).toBe(startHealth - 5);
	});

	it('sums every DoT type in fixed order', () => {
		// Bleed 50→2, Poison 100→4, Burn 25→1 = 7 total, no resistance authored.
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.BleedDamagePerSecond, amount: 50 },
			{ id: EAttribute.PoisonDamagePerSecond, amount: 100 },
			{ id: EAttribute.BurnDamagePerSecond, amount: 25 }
		]);
		const startHealth = battler.currentHealth;

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(7);
		expect(battler.currentHealth).toBe(startHealth - 7);
	});

	it('mitigates every DoT type with the DoT cross-cutting resistance', () => {
		// DotResistance 0.5 resists all three at once: Bleed 2, Poison 4, Burn 1 each × (1 − 0.5) → 3.5.
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 },
			{ id: EAttribute.BleedDamagePerSecond, amount: 50 },
			{ id: EAttribute.PoisonDamagePerSecond, amount: 100 },
			{ id: EAttribute.BurnDamagePerSecond, amount: 25 },
			{ id: EAttribute.DotResistance, amount: 0.5 }
		]);

		const dealt = battler.applyDamageOverTime(40);

		expect(dealt).toBe(3.5);
	});

	it('scales HealthRegenPerSecond by the tick', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 }, // MaxHealth 100
			{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
		]);
		battler.takeDamage(52, EDamageType.Physical); // Defense 2 → 50 damage → currentHealth 50

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
		battler.takeDamage(4, EDamageType.Physical); // Defense 2 → 2 damage → currentHealth 98

		const healed = battler.applyHealOverTime(40); // would heal 3, but only 2 of room remains

		expect(healed).toBe(2);
		expect(battler.currentHealth).toBe(100);
	});

	it('keeps isDead in sync after a heal (matches the backend always-live IsDead)', () => {
		const battler = makeBattler([
			{ id: EAttribute.Strength, amount: 10 }, // MaxHealth 100
			{ id: EAttribute.HealthRegenPerSecond, amount: 75 }
		]);
		battler.takeDamage(52, EDamageType.Physical); // currentHealth 50

		battler.applyHealOverTime(40); // heals 3 → currentHealth 53

		expect(battler.isDead).toBe(battler.currentHealth <= 0);
		expect(battler.isDead).toBe(false);
	});
});
