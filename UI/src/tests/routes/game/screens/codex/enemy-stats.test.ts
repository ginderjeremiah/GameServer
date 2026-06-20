import { describe, it, expect, vi } from 'vitest';
import { EAttribute, type IEnemy } from '$lib/api';

// enemy-stats composes BattleAttributes, which reads attribute names from the staticData store.
vi.mock('$stores', () => ({ staticData: {} }));

import { enemyAttributesAtLevel } from '$routes/game/screens/codex/enemy-stats';
import { BattleAttributes } from '$lib/battle/battle-attributes';

const distribution = [
	{ attributeId: EAttribute.Strength, baseAmount: 12, amountPerLevel: 1.4 },
	{ attributeId: EAttribute.Endurance, baseAmount: 10, amountPerLevel: 1.1 },
	{ attributeId: EAttribute.Intellect, baseAmount: 4, amountPerLevel: 0.3 },
	{ attributeId: EAttribute.Agility, baseAmount: 8, amountPerLevel: 0.9 },
	{ attributeId: EAttribute.Dexterity, baseAmount: 6, amountPerLevel: 0.6 },
	{ attributeId: EAttribute.Luck, baseAmount: 3, amountPerLevel: 0.2 }
];

const makeEnemy = (): IEnemy =>
	({
		id: 0,
		name: 'Marauder',
		isBoss: false,
		attributeDistribution: distribution,
		skillPool: [],
		spawns: []
	}) as IEnemy;

describe('enemyAttributesAtLevel', () => {
	it('scales each primary attribute by base + perLevel × level', () => {
		const { primary } = enemyAttributesAtLevel(makeEnemy(), 20);
		const str = primary.find((p) => p.attributeId === EAttribute.Strength)!;
		expect(str).toMatchObject({ base: 12, perLevel: 1.4 });
		expect(str.value).toBe(Math.round(12 + 1.4 * 20)); // 40
		expect(primary).toHaveLength(6);
	});

	it('derives Max Health and Defense (absent from the distribution) via the battle composition', () => {
		const enemy = makeEnemy();
		const { secondary } = enemyAttributesAtLevel(enemy, 20);
		expect(secondary.map((s) => s.attributeId)).toEqual([EAttribute.MaxHealth, EAttribute.Defense]);

		// Matches the canonical BattleAttributes build the battle uses.
		const expected = new BattleAttributes(
			distribution.map((d) => ({ attributeId: d.attributeId, amount: d.baseAmount + d.amountPerLevel * 20 })),
			true
		);
		expect(secondary[0].value).toBe(Math.round(expected.getValue(EAttribute.MaxHealth)));
		expect(secondary[1].value).toBe(Math.round(expected.getValue(EAttribute.Defense)));
		expect(secondary[0].value).toBeGreaterThan(0);
		expect(secondary[1].value).toBeGreaterThan(0);
	});

	it('exposes a constant per-level slope for derived stats (linear in level)', () => {
		const enemy = makeEnemy();
		const a = enemyAttributesAtLevel(enemy, 5).secondary;
		const b = enemyAttributesAtLevel(enemy, 30).secondary;
		expect(a[0].perLevel).toBeCloseTo(b[0].perLevel, 6);
		expect(a[1].perLevel).toBeCloseTo(b[1].perLevel, 6);
		expect(a[0].perLevel).toBeGreaterThan(0);
	});

	it('memoises the result per enemy + level', () => {
		const enemy = makeEnemy();
		expect(enemyAttributesAtLevel(enemy, 12)).toBe(enemyAttributesAtLevel(enemy, 12));
		expect(enemyAttributesAtLevel(enemy, 12)).not.toBe(enemyAttributesAtLevel(enemy, 13));
	});
});
