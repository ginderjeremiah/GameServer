import { describe, it, expect } from 'vitest';
import { EAttribute, type IBattlerAttribute } from '$lib/api';
import {
	BattleAttributes,
	computeAttributes,
	foldAttributeValue,
	groupBySource,
	SOURCE_ORDER,
	STATIC_ATTRIBUTE_MODIFIERS,
	EModifierType,
	EAttributeModifierSource,
	type AttributeModifier
} from '$lib/battle';

/** A `Derived` modifier scaling `amount` by the final value of `derivedSource`. */
const derived = (attribute: EAttribute, amount: number, derivedSource: EAttribute): AttributeModifier => ({
	attribute,
	amount,
	type: EModifierType.Additive,
	source: EAttributeModifierSource.Derived,
	derivedSource
});

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
	source: Exclude<
		EAttributeModifierSource,
		EAttributeModifierSource.Derived
	> = EAttributeModifierSource.PlayerStatPoints
): AttributeModifier => ({ attribute, amount, type: EModifierType.Additive, source });

/** The highest attribute id, for iterating every slot the way BattleAttributes' value array does. */
const maxAttributeId = Math.max(...Object.values(EAttribute).filter((v): v is number => typeof v === 'number'));

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

		const enduranceLine = hp.lines.find(
			(l) => l.source === EAttributeModifierSource.Derived && l.derivedSource === EAttribute.Endurance
		)!;
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

	// Crit is a per-skill opt-in enabler (crit rework #1425, per-skill base #1453): the ENABLER is a skill's
	// own authored CriticalChance (0 unless authored), and CriticalChanceMultiplier only scales that base —
	// so, unlike the other chance attributes, it carries a base of 1 (like CooldownRecovery/CriticalDamage),
	// never 0. These two mirror Game.Core.Tests AttributeCollectionTests.
	it('leaves CriticalChanceMultiplier at its base 1 with no further investment', () => {
		// With no further investment the multiplier is exactly 1, so a committed skill still crits at its
		// own authored rate with zero additional cost.
		const computed = computeAttributes(withStatics([]));
		expect(computed.get(EAttribute.CriticalChanceMultiplier)!.total).toBe(1);
	});

	it('stacks an additive Precision/gear bonus onto the CriticalChanceMultiplier base', () => {
		// A Precision/gear bonus composes additively onto the base 1, exactly like CooldownRecovery.
		const computed = computeAttributes(withStatics([additive(EAttribute.CriticalChanceMultiplier, 0.5)]));
		expect(computed.get(EAttribute.CriticalChanceMultiplier)!.total).toBeCloseTo(1.5, 10);
	});

	it('returns a zero total with no lines for an attribute with no modifiers', () => {
		const computed = computeAttributes([additive(EAttribute.Strength, 5)]);
		// Intellect was never referenced, so it is simply absent.
		expect(computed.get(EAttribute.Intellect)).toBeUndefined();
	});

	it('throws for an unsupported modifier type', () => {
		// EModifierType only defines Additive(1) and Multiplicative(2); an out-of-range value
		// represents a programming error and must surface loudly rather than silently no-op.
		const modifier: AttributeModifier = {
			attribute: EAttribute.Strength,
			amount: 5,
			type: 99 as EModifierType,
			source: EAttributeModifierSource.Item
		};
		expect(() => computeAttributes([modifier])).toThrow('Unsupported EModifierType: 99');
	});

	it('treats a circular derived chain as zero instead of recursing forever', () => {
		// Intellect derives from Luck and Luck from Intellect: the in-progress guard breaks the cycle by
		// treating a re-entered attribute as 0, so both resolve to 0 rather than overflowing the stack.
		const computed = computeAttributes([
			derived(EAttribute.Intellect, 2, EAttribute.Luck),
			derived(EAttribute.Luck, 3, EAttribute.Intellect)
		]);
		expect(computed.get(EAttribute.Intellect)?.total).toBe(0);
		expect(computed.get(EAttribute.Luck)?.total).toBe(0);
	});
});

describe('foldAttributeValue', () => {
	it('applies additives then multiplicatives, scaling a derived modifier by its source', () => {
		// running = (4 + 2*Source(10)) then *1.5 = 24 * 1.5 = 36.
		const value = foldAttributeValue(
			[
				additive(EAttribute.Strength, 4),
				derived(EAttribute.Strength, 2, EAttribute.Luck),
				{
					attribute: EAttribute.Strength,
					amount: 1.5,
					type: EModifierType.Multiplicative,
					source: EAttributeModifierSource.ItemMod
				}
			],
			(attr) => (attr === EAttribute.Luck ? 10 : 0)
		);
		expect(value).toBe(36);
	});

	it('throws for an unsupported modifier type', () => {
		const modifier: AttributeModifier = {
			attribute: EAttribute.Strength,
			amount: 5,
			type: 99 as EModifierType,
			source: EAttributeModifierSource.Item
		};
		expect(() => foldAttributeValue([modifier], () => 0)).toThrow('Unsupported EModifierType: 99');
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
			additive(EAttribute.Toughness, 6, EAttributeModifierSource.ItemMod),
			additive(EAttribute.Toughness, 14, EAttributeModifierSource.Item)
		]);
		const { groups } = groupBySource(computed.get(EAttribute.Toughness)!);
		const positions = groups.map((g) => SOURCE_ORDER.indexOf(g.source));
		expect(positions).toEqual([...positions].sort((a, b) => a - b));
	});
});

/* ── parity with the battle model ─────────────────────────────────────────────
   `BattleAttributes` recomputes incrementally through the shared `foldAttributeValue`
   kernel rather than rebuilding via `computeAttributes`, but both fold the same
   `STATIC_ATTRIBUTE_MODIFIERS` the same way. These cases are a regression guard that the
   two stay equal: they verify `BattleAttributes`' totals match a full `computeAttributes`
   so a change to the kernel or the static modifiers can't silently diverge the simulation. */
describe('parity with BattleAttributes', () => {
	const IMPLEMENTED: EAttribute[] = [
		EAttribute.Strength,
		EAttribute.Endurance,
		EAttribute.Intellect,
		EAttribute.Agility,
		EAttribute.Dexterity,
		EAttribute.Luck,
		EAttribute.MaxHealth,
		EAttribute.Toughness,
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
				{ attributeId: EAttribute.Toughness, amount: 14 },
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

	// BattleAttributes now recomputes incrementally (only the changed attribute + its derived
	// dependents) rather than rebuilding from the full modifier list. This guards that the incremental
	// add/remove path never drifts from a fresh, full `computeAttributes` over the same modifier set —
	// the FE-internal invariant #1364's optimization rests on. Every step exercises a derived cascade
	// (Strength/Endurance → MaxHealth, Endurance → Toughness, Luck → Critical*).
	it('incremental add/remove stays equal to a full recompute at every step', () => {
		const battle = new BattleAttributes([], true);
		const live: AttributeModifier[] = [];

		// A full, independent recompute of every attribute from the current modifier set.
		const expectEquivalent = () => {
			const computed = computeAttributes(withStatics(live));
			for (let attr = 0; attr <= maxAttributeId; attr++) {
				expect(battle.getValue(attr)).toBeCloseTo(computed.get(attr)?.total ?? 0, 10);
			}
		};

		const apply = (modifier: AttributeModifier) => {
			battle.addModifier(modifier);
			live.push(modifier);
			expectEquivalent();
		};
		const revert = (modifier: AttributeModifier) => {
			expect(battle.removeModifier(modifier)).toBe(true);
			live.splice(live.indexOf(modifier), 1);
			expectEquivalent();
		};

		const strength = additive(EAttribute.Strength, 12, EAttributeModifierSource.Item);
		const endurance = additive(EAttribute.Endurance, 8, EAttributeModifierSource.PlayerStatPoints);
		const luck = additive(EAttribute.Luck, 20, EAttributeModifierSource.ItemMod);
		const directHealth = additive(EAttribute.MaxHealth, 35, EAttributeModifierSource.Item);
		const multHealth: AttributeModifier = {
			attribute: EAttribute.MaxHealth,
			amount: 1.1,
			type: EModifierType.Multiplicative,
			source: EAttributeModifierSource.ItemMod
		};

		expectEquivalent();
		apply(strength); // cascades Strength → MaxHealth
		apply(endurance); // cascades Endurance → MaxHealth + Toughness
		apply(luck); // cascades Luck → CriticalDamage + the crit/parry multipliers (#1525)
		apply(directHealth); // a non-derived MaxHealth additive alongside its derived contributors
		apply(multHealth); // a multiplicative on the same attribute, applied after the additives
		revert(endurance); // drop a shared cascade source mid-stack
		revert(multHealth);
		revert(strength);
		revert(directHealth);
		revert(luck);
	});
});
