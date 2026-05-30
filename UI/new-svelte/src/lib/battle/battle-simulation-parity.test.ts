import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import { BattleAttributes } from './battle-attributes';

/**
 * Pure simulation loop that mirrors the backend BattleSimulator exactly.
 * No runtime dependencies — just math.
 */
function simulateBattle(player: SimBattler, enemy: SimBattler): number {
	const msPerTick = 40;
	const maxMs = msPerTick * 10000;

	for (let totalMs = msPerTick; totalMs <= maxMs; totalMs += msPerTick) {
		updateBattler(player, enemy, msPerTick);
		if (enemy.currentHealth <= 0) return totalMs;

		updateBattler(enemy, player, msPerTick);
		if (player.currentHealth <= 0) return totalMs;
	}
	return maxMs + msPerTick;
}

function updateBattler(attacker: SimBattler, defender: SimBattler, timeDelta: number) {
	for (const skill of attacker.skills) {
		skill.chargeTime += timeDelta * attacker.cdMultiplier;
		if (skill.chargeTime >= skill.cooldownMs) {
			skill.chargeTime = 0;
			const rawDmg =
				skill.baseDamage +
				skill.multipliers.reduce((sum, m) => sum + attacker.attributes.getValue(m.attributeId) * m.amount, 0);
			let dmg = rawDmg - defender.attributes.getValue(EAttribute.Defense);
			if (dmg < 0) dmg = 0;
			defender.currentHealth -= dmg;
		}
	}
}

interface SimSkill {
	baseDamage: number;
	cooldownMs: number;
	chargeTime: number;
	multipliers: { attributeId: EAttribute; amount: number }[];
}

interface SimBattler {
	attributes: BattleAttributes;
	currentHealth: number;
	cdMultiplier: number;
	skills: SimSkill[];
}

function makeSkill(
	baseDamage: number,
	cooldownMs: number,
	multipliers: { attributeId: EAttribute; amount: number }[] = []
): SimSkill {
	return { baseDamage, cooldownMs, chargeTime: 0, multipliers };
}

/**
 * Creates a battler from RAW stat allocations (like the backend does).
 * Derived stats are calculated once by BattleAttributes.
 */
function makeBattlerFromRaw(attrs: { id: EAttribute; amount: number }[], skills: SimSkill[]): SimBattler {
	const battlerAttrs = attrs.map((a) => ({ attributeId: a.id, amount: a.amount }));
	const ba = new BattleAttributes(battlerAttrs, true);
	return {
		attributes: ba,
		currentHealth: ba.getValue(EAttribute.MaxHealth),
		cdMultiplier: 1 + ba.getValue(EAttribute.CooldownRecovery) / 100,
		skills
	};
}

/**
 * Creates a battler from FINAL attribute values (as the API sends for the player),
 * then recalculates derived stats — reproducing the double-counting bug.
 */
function makeBattlerFromFinalValues(attrs: { id: EAttribute; amount: number }[], skills: SimSkill[]): SimBattler {
	const battlerAttrs = attrs.map((a) => ({ attributeId: a.id, amount: a.amount }));
	// calcDerivedStats=true adds derived stats on top of the already-final values
	const ba = new BattleAttributes(battlerAttrs, true);
	return {
		attributes: ba,
		currentHealth: ba.getValue(EAttribute.MaxHealth),
		cdMultiplier: 1 + ba.getValue(EAttribute.CooldownRecovery) / 100,
		skills
	};
}

// ── Test scenario ────────────────────────────────────────────────────────────
// Must match Game.Core.Tests.Battle.BattleSimulatorParityTests exactly.
//
// Player raw stats: Str=50, End=30, Agi=20, Dex=10
//   Derived: MaxHealth=900, Defense=42, CooldownRecovery=9
// Player skill: BaseDmg=10, {Str*1.5}, CD=1200
//
// Enemy raw stats: Str=10, End=15
//   Derived: MaxHealth=400, Defense=17, CooldownRecovery=0
// Enemy skill: BaseDmg=5, no multipliers, CD=2000

const playerRawAttrs = [
	{ id: EAttribute.Strength, amount: 50 },
	{ id: EAttribute.Endurance, amount: 30 },
	{ id: EAttribute.Agility, amount: 20 },
	{ id: EAttribute.Dexterity, amount: 10 }
];

const enemyRawAttrs = [
	{ id: EAttribute.Strength, amount: 10 },
	{ id: EAttribute.Endurance, amount: 15 }
];

function playerSkill(): SimSkill {
	return makeSkill(10, 1200, [{ attributeId: EAttribute.Strength, amount: 1.5 }]);
}

function enemySkill(): SimSkill {
	return makeSkill(5, 2000);
}

// ── Tests ────────────────────────────────────────────────────────────────────

describe('Battle simulation parity with backend', () => {
	it('produces the correct totalMs with raw stat allocations (should match backend)', () => {
		const player = makeBattlerFromRaw(playerRawAttrs, [playerSkill()]);
		const enemy = makeBattlerFromRaw(enemyRawAttrs, [enemySkill()]);

		// Verify derived stats match expected values
		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(900);
		expect(player.attributes.getValue(EAttribute.Defense)).toBe(42);
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBe(9);
		expect(player.cdMultiplier).toBeCloseTo(1.09, 10);

		expect(enemy.attributes.getValue(EAttribute.MaxHealth)).toBe(400);
		expect(enemy.attributes.getValue(EAttribute.Defense)).toBe(17);

		const totalMs = simulateBattle(player, enemy);

		// Must match the backend's BattleSimulatorParityTests.Parity_WithCooldownRecovery_MatchesExpectedTotalMs
		expect(totalMs).toBe(6720);
	});

	it('demonstrates the double-counting bug: final API values + re-derived stats', () => {
		// Compute the final attribute values as the API would send them
		// (this is what PlayerData.FromPlayer produces)
		const playerFinalAttrs = [
			{ id: EAttribute.Strength, amount: 50 },
			{ id: EAttribute.Endurance, amount: 30 },
			{ id: EAttribute.Agility, amount: 20 },
			{ id: EAttribute.Dexterity, amount: 10 },
			// These are the FINAL values including derived stats:
			{ id: EAttribute.MaxHealth, amount: 900 }, // 50 + 20*30 + 5*50
			{ id: EAttribute.Defense, amount: 42 }, // 2 + 30 + 0.5*20
			{ id: EAttribute.CooldownRecovery, amount: 9 } // 0.4*20 + 0.1*10
		];

		// This is how the frontend currently constructs the player battler:
		// it passes the final API values to BattleAttributes with calcDerivedStats=true,
		// causing derived stats to be added AGAIN on top of the already-computed values.
		const player = makeBattlerFromFinalValues(playerFinalAttrs, [playerSkill()]);
		const enemy = makeBattlerFromRaw(enemyRawAttrs, [enemySkill()]);

		// Derived stats are now DOUBLED
		expect(player.attributes.getValue(EAttribute.MaxHealth)).toBe(1800); // 900 + 900
		expect(player.attributes.getValue(EAttribute.Defense)).toBe(84); // 42 + 42
		expect(player.attributes.getValue(EAttribute.CooldownRecovery)).toBe(18); // 9 + 9
		expect(player.cdMultiplier).toBeCloseTo(1.18, 10);

		const totalMs = simulateBattle(player, enemy);

		// Battle ends sooner because player has inflated stats
		expect(totalMs).toBe(6240);
		expect(totalMs).toBeLessThan(6720);
	});

	it('the difference matches the ~300ms discrepancy range', () => {
		const playerCorrect = makeBattlerFromRaw(playerRawAttrs, [playerSkill()]);
		const enemyA = makeBattlerFromRaw(enemyRawAttrs, [enemySkill()]);
		const correctMs = simulateBattle(playerCorrect, enemyA);

		const playerFinalAttrs = [
			...playerRawAttrs,
			{ id: EAttribute.MaxHealth, amount: 900 },
			{ id: EAttribute.Defense, amount: 42 },
			{ id: EAttribute.CooldownRecovery, amount: 9 }
		];
		const playerBugged = makeBattlerFromFinalValues(playerFinalAttrs, [playerSkill()]);
		const enemyB = makeBattlerFromRaw(enemyRawAttrs, [enemySkill()]);
		const buggedMs = simulateBattle(playerBugged, enemyB);

		const differenceMs = correctMs - buggedMs;
		expect(differenceMs).toBe(480);
		expect(differenceMs).toBeGreaterThanOrEqual(200);
		expect(differenceMs).toBeLessThanOrEqual(600);
	});
});
