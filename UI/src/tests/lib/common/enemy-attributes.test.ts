import { describe, it, expect, vi } from 'vitest';
import { EAttribute, type IEnemy } from '$lib/api';

// enemy-attributes composes BattleAttributes, which reads attribute names from the staticData store.
vi.mock('$stores', () => ({ staticData: {} }));

import { enemyAttributesAtLevel, enemyBattleAttributes, enemyDefense } from '$lib/common/enemy-attributes';
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

/* A leaner enemy whose Defense resolves to a clean value: Defense = 2 (base) + 1·Endurance +
   0.5·Agility, so with only per-level Endurance, Defense = 2 + amountPerLevel·level. */
const enemy = (over: Partial<IEnemy> & { id: number }): IEnemy =>
	({
		name: `Enemy ${over.id}`,
		isBoss: false,
		attributeDistribution: [],
		skillPool: [],
		spawns: [],
		...over
	}) as IEnemy;

const enduranceDist = (amountPerLevel: number) => [
	{ attributeId: EAttribute.Endurance, baseAmount: 0, amountPerLevel }
];

describe('enemyBattleAttributes', () => {
	it('builds the canonical BattleAttributes the battle uses (additive distribution at the level)', () => {
		const built = enemyBattleAttributes(makeEnemy(), 20);
		const expected = new BattleAttributes(
			distribution.map((d) => ({ attributeId: d.attributeId, amount: d.baseAmount + d.amountPerLevel * 20 })),
			true
		);
		expect(built.getValue(EAttribute.Defense)).toBe(expected.getValue(EAttribute.Defense));
		expect(built.getValue(EAttribute.MaxHealth)).toBe(expected.getValue(EAttribute.MaxHealth));
	});

	it('memoises the build per enemy + level', () => {
		const e = makeEnemy();
		expect(enemyBattleAttributes(e, 7)).toBe(enemyBattleAttributes(e, 7));
		expect(enemyBattleAttributes(e, 7)).not.toBe(enemyBattleAttributes(e, 8));
	});
});

describe('enemyDefense', () => {
	it('resolves an enemy Defense through the derived attribute composition', () => {
		// Defense = 2 (base) + 1·Endurance + 0.5·Agility; these enemies carry only per-level Endurance.
		expect(enemyDefense(enemy({ id: 0, attributeDistribution: enduranceDist(2) }), 5)).toBe(12); // 2 + 2*5
		expect(enemyDefense(enemy({ id: 1, attributeDistribution: enduranceDist(3) }), 10)).toBe(32); // 2 + 3*10
	});

	it('memoises enemy defense per enemy and level', () => {
		const e = enemy({ id: 99, attributeDistribution: enduranceDist(4) });
		expect(enemyDefense(e, 5)).toBe(22); // 2 + 4*5

		// The result is cached by enemy identity, so a later mutation of the (in practice immutable)
		// distribution is not observed at the cached level — proving it is served from the memo.
		e.attributeDistribution = enduranceDist(100);
		expect(enemyDefense(e, 5)).toBe(22);
		// A different level is a distinct cache entry and reflects the current distribution.
		expect(enemyDefense(e, 1)).toBe(102); // 2 + 100*1
	});

	it('delegates to the same build as enemyAttributesAtLevel (the single enemy battle source)', () => {
		const e = makeEnemy();
		const defenseSecondary = enemyAttributesAtLevel(e, 14).secondary.find((s) => s.attributeId === EAttribute.Defense);
		// enemyDefense is the raw build value; the dossier rounds it for display — so they agree once rounded.
		expect(Math.round(enemyDefense(e, 14))).toBe(defenseSecondary?.value);
	});
});

describe('enemyAttributesAtLevel', () => {
	it('scales each primary attribute by base + perLevel × level', () => {
		const { primary } = enemyAttributesAtLevel(makeEnemy(), 20);
		const str = primary.find((p) => p.attributeId === EAttribute.Strength);
		expect(str).toMatchObject({ base: 12, perLevel: 1.4 });
		expect(str?.value).toBe(Math.round(12 + 1.4 * 20)); // 40
		expect(primary).toHaveLength(6);
	});

	it('derives Max Health and Defense (absent from the distribution) via the battle composition', () => {
		const enemyRecord = makeEnemy();
		const { secondary } = enemyAttributesAtLevel(enemyRecord, 20);
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
		const enemyRecord = makeEnemy();
		const a = enemyAttributesAtLevel(enemyRecord, 5).secondary;
		const b = enemyAttributesAtLevel(enemyRecord, 30).secondary;
		expect(a[0].perLevel).toBeCloseTo(b[0].perLevel, 6);
		expect(a[1].perLevel).toBeCloseTo(b[1].perLevel, 6);
		expect(a[0].perLevel).toBeGreaterThan(0);
	});

	it('memoises the result per enemy + level', () => {
		const enemyRecord = makeEnemy();
		expect(enemyAttributesAtLevel(enemyRecord, 12)).toBe(enemyAttributesAtLevel(enemyRecord, 12));
		expect(enemyAttributesAtLevel(enemyRecord, 12)).not.toBe(enemyAttributesAtLevel(enemyRecord, 13));
	});
});
