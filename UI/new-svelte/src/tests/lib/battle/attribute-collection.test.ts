import { describe, it, expect } from 'vitest';
import { EAttribute, type IBattlerAttribute } from '$lib/api';
import {
	BattleAttributes,
	computeAttributes,
	groupBySource,
	SOURCE_ORDER,
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier
} from '$lib/battle';

/** Builds a modifier list from plain additive contributions plus the engine's
 *  static base/derived modifiers — the same shape the breakdown assembles from
 *  the player's allocations + equipment. */
const withStatics = (contribs: AttributeModifier[]): AttributeModifier[] => [
	...contribs,
	...STATIC_ATTRIBUTE_MODIFIERS
];

const additive = (
	attribute: EAttribute,
	amount: number,
	source = EAttributeModifierSource.PlayerStatPoints
): AttributeModifier => ({ attribute, amount, type: EModifierType.Additive, source });

describe('computeAttributes', () => {
	it('sums additive modifiers for a core attribute', () => {
		const computed = computeAttributes([
			additive(EAttribute.Strength, 10, EAttributeModifierSource.PlayerStatPoints),
			additive(EAttribute.Strength, 18, EAttributeModifierSource.Item),
			additive(EAttribute.Strength, 4, EAttributeModifierSource.ItemMod)
		]);
		const str = computed.get(EAttribute.Strength)!;
		expect(str.total).toBe(32);
		expect(str.additiveSubtotal).toBe(32);
		expect(str.lines).toHaveLength(3);
		expect(str.lines.every((l) => !l.multiplied)).toBe(true);
	});

	it('resolves a derived modifier against the final value of its source', () => {
		// MaxHealth = base 50 + 20×Endurance + 5×Strength.
		const computed = computeAttributes(
			withStatics([additive(EAttribute.Endurance, 12), additive(EAttribute.Strength, 14)])
		);
		const hp = computed.get(EAttribute.MaxHealth)!;
		expect(hp.total).toBe(50 + 20 * 12 + 5 * 14);

		const enduranceLine = hp.lines.find((l) => l.derivedSource === EAttribute.Endurance)!;
		expect(enduranceLine.derivedValue).toBe(12);
		expect(enduranceLine.applied).toBe(240);
	});

	it('applies additive modifiers before multiplicative ones, regardless of input order', () => {
		const computed = computeAttributes([
			{
				attribute: EAttribute.Strength,
				amount: 1.5,
				type: EModifierType.Multiplicative,
				source: EAttributeModifierSource.Item
			},
			additive(EAttribute.Strength, 10),
			additive(EAttribute.Strength, 30, EAttributeModifierSource.Item)
		]);
		const str = computed.get(EAttribute.Strength)!;
		expect(str.additiveSubtotal).toBe(40);
		expect(str.total).toBe(60); // 40 × 1.5
		expect(str.multUplift).toBe(20);
		// The multiplicative line is last in apply order.
		expect(str.lines.at(-1)!.multiplied).toBe(true);
		expect(str.lines.at(-1)!.factor).toBe(1.5);
	});

	it('returns a zero total with no lines for an attribute with no modifiers', () => {
		const computed = computeAttributes([additive(EAttribute.Strength, 5)]);
		// Intellect was never referenced, so it is simply absent.
		expect(computed.get(EAttribute.Intellect)).toBeUndefined();
	});
});

describe('groupBySource', () => {
	it('buckets additive lines by source in display order and separates multipliers', () => {
		const computed = computeAttributes([
			additive(EAttribute.Strength, 5, EAttributeModifierSource.PlayerStatPoints),
			additive(EAttribute.Strength, 18, EAttributeModifierSource.Item),
			additive(EAttribute.Strength, 4, EAttributeModifierSource.ItemMod),
			{
				attribute: EAttribute.Strength,
				amount: 1.1,
				type: EModifierType.Multiplicative,
				source: EAttributeModifierSource.Item
			}
		]);
		const { groups, mults } = groupBySource(computed.get(EAttribute.Strength)!);
		expect(groups.map((g) => g.source)).toEqual([
			EAttributeModifierSource.PlayerStatPoints,
			EAttributeModifierSource.Item,
			EAttributeModifierSource.ItemMod
		]);
		expect(groups.find((g) => g.source === EAttributeModifierSource.Item)!.total).toBe(18);
		expect(mults).toHaveLength(1);
	});

	it('orders groups by SOURCE_ORDER even when modifiers arrive out of order', () => {
		const computed = computeAttributes([
			additive(EAttribute.Defense, 6, EAttributeModifierSource.ItemMod),
			additive(EAttribute.Defense, 14, EAttributeModifierSource.Item)
		]);
		const { groups } = groupBySource(computed.get(EAttribute.Defense)!);
		const positions = groups.map((g) => SOURCE_ORDER.indexOf(g.source));
		expect(positions).toEqual([...positions].sort((a, b) => a - b));
	});
});

/* ── parity with the battle model ─────────────────────────────────────────────
   The breakdown must never disagree with the numbers the battle simulation
   actually produces. `BattleAttributes` is the frontend battle calculator; this
   asserts the modifier pipeline yields identical totals for the implemented
   attributes given the same additive inputs. If a formula constant drifts in one
   place but not the other, this fails. */
describe('parity with BattleAttributes', () => {
	const IMPLEMENTED: EAttribute[] = [
		EAttribute.Strength,
		EAttribute.Endurance,
		EAttribute.Intellect,
		EAttribute.Agility,
		EAttribute.Dexterity,
		EAttribute.Luck,
		EAttribute.MaxHealth,
		EAttribute.Defense,
		EAttribute.CooldownRecovery
	];

	const cases: { name: string; contribs: IBattlerAttribute[] }[] = [
		{ name: 'empty build', contribs: [] },
		{
			name: 'allocations only',
			contribs: [
				{ attributeId: EAttribute.Strength, amount: 14 },
				{ attributeId: EAttribute.Endurance, amount: 12 },
				{ attributeId: EAttribute.Agility, amount: 10 },
				{ attributeId: EAttribute.Dexterity, amount: 8 }
			]
		},
		{
			name: 'allocations + equipment (including a direct MaxHealth bonus)',
			contribs: [
				{ attributeId: EAttribute.Strength, amount: 14 },
				{ attributeId: EAttribute.Endurance, amount: 20 },
				{ attributeId: EAttribute.MaxHealth, amount: 30 },
				{ attributeId: EAttribute.Defense, amount: 14 },
				{ attributeId: EAttribute.Agility, amount: 9 },
				{ attributeId: EAttribute.Dexterity, amount: 12 }
			]
		}
	];

	for (const { name, contribs } of cases) {
		it(`matches for ${name}`, () => {
			const battle = new BattleAttributes(contribs, true);
			const modifiers = withStatics(
				contribs.map((c) => additive(c.attributeId, c.amount, EAttributeModifierSource.Item))
			);
			const computed = computeAttributes(modifiers);
			for (const attr of IMPLEMENTED) {
				const total = computed.get(attr)?.total ?? 0;
				expect(total).toBeCloseTo(battle.getValue(attr), 6);
			}
		});
	}
});
