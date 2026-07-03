import { describe, it, expect, vi } from 'vitest';
import { EAttribute, EModifierType, type IAttribute } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import { EAttributeModifierSource, type AttributeModifier } from '$lib/battle/attribute-modifier';
import {
	applySignaturePassive,
	composePlayerBattleAttributes,
	playerBattleModifiers
} from '$lib/battle/player-battle-composition';

// BattleAttributes resolves attribute names through the static-data store; an empty mock is enough here
// since these assertions read raw values, not names.
const { mockAttributes } = vi.hoisted(() => ({ mockAttributes: [] as IAttribute[] }));
vi.mock('$stores', () => ({
	staticData: {
		get attributes() {
			return mockAttributes;
		}
	}
}));

const makeAttrs = (...pairs: [EAttribute, number][]) => pairs.map(([attributeId, amount]) => ({ attributeId, amount }));

const additive = (
	attribute: EAttribute,
	amount: number,
	source: Exclude<EAttributeModifierSource, EAttributeModifierSource.Derived>
): AttributeModifier => ({
	attribute,
	amount,
	type: EModifierType.Additive,
	source
});

describe('playerBattleModifiers', () => {
	it('concatenates the locked base before the proficiency bonuses', () => {
		const lockedBase = [additive(EAttribute.Strength, 5, EAttributeModifierSource.AttributeDistribution)];
		const proficiency = [additive(EAttribute.Strength, 2, EAttributeModifierSource.Proficiency)];
		expect(playerBattleModifiers(lockedBase, proficiency)).toEqual([...lockedBase, ...proficiency]);
	});

	it('returns an empty array when both inputs are empty', () => {
		expect(playerBattleModifiers([], [])).toEqual([]);
	});
});

describe('applySignaturePassive', () => {
	it('adds a flat passive to the supplied set', () => {
		const attrs = new BattleAttributes();
		attrs.setData(makeAttrs([EAttribute.Strength, 10]), true, []);
		applySignaturePassive(attrs, () => additive(EAttribute.Strength, 4, EAttributeModifierSource.Class));
		expect(attrs.getValue(EAttribute.Strength)).toBe(14);
	});

	it('resolves an attribute-scaled passive against the set it is applied to', () => {
		const attrs = new BattleAttributes();
		attrs.setData(makeAttrs([EAttribute.Endurance, 6]), true, [
			additive(EAttribute.Endurance, 2, EAttributeModifierSource.AttributeDistribution)
		]);
		applySignaturePassive(attrs, (resolveScalingValue) =>
			additive(EAttribute.Luck, resolveScalingValue(EAttribute.Endurance) * 2, EAttributeModifierSource.Class)
		);
		expect(attrs.getValue(EAttribute.Luck)).toBe(16); // 8 (resolved Endurance) × 2
	});
});

describe('composePlayerBattleAttributes', () => {
	it('composes allocation, equipped gear, locked base, and proficiency into the base set', () => {
		const attrs = composePlayerBattleAttributes(
			makeAttrs([EAttribute.Strength, 10]),
			makeAttrs([EAttribute.Strength, 4]),
			[additive(EAttribute.Endurance, 3, EAttributeModifierSource.AttributeDistribution)],
			[additive(EAttribute.Endurance, 2, EAttributeModifierSource.Proficiency)],
			() => ({
				attribute: EAttribute.Strength,
				amount: 0,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Class
			})
		);
		expect(attrs.getValue(EAttribute.Strength)).toBe(14); // 10 alloc + 4 gear
		expect(attrs.getValue(EAttribute.Endurance)).toBe(5); // 3 locked base + 2 proficiency
	});

	it('resolves the signature passive against the fully-assembled set, composed last', () => {
		// A passive scaling off Endurance must read Endurance's value AFTER the locked base/proficiency/statics
		// are folded in — exactly the live battler's two-pass assembly (build the set, then add the passive
		// reading the resolved scaling value). This also exercises the resolver callback threaded through
		// `resolveSignaturePassive`, proving it is actually invoked with the assembled attributes.
		const attrs = composePlayerBattleAttributes(
			makeAttrs([EAttribute.Endurance, 5]),
			[],
			[additive(EAttribute.Endurance, 3, EAttributeModifierSource.AttributeDistribution)],
			[],
			(resolveScalingValue) => ({
				attribute: EAttribute.Luck,
				amount: resolveScalingValue(EAttribute.Endurance) * 2,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Class
			})
		);
		expect(attrs.getValue(EAttribute.Endurance)).toBe(8); // 5 alloc + 3 locked base
		expect(attrs.getValue(EAttribute.Luck)).toBe(16); // 8 (resolved Endurance) × 2
	});

	it('matches manual setData + addModifier composition (the production path this replaces)', () => {
		const allocations = makeAttrs([EAttribute.Strength, 7]);
		const equipmentStats = makeAttrs([EAttribute.Strength, 1]);
		const lockedBase = [additive(EAttribute.Strength, 2, EAttributeModifierSource.AttributeDistribution)];
		const proficiency = [additive(EAttribute.Strength, 1, EAttributeModifierSource.Proficiency)];
		const passive = (resolve: (attribute: EAttribute) => number): AttributeModifier => ({
			attribute: EAttribute.Strength,
			amount: resolve(EAttribute.Strength) > 0 ? 3 : 0,
			type: EModifierType.Additive,
			source: EAttributeModifierSource.Class
		});

		const composed = composePlayerBattleAttributes(allocations, equipmentStats, lockedBase, proficiency, passive);

		const manual = new BattleAttributes();
		manual.setData([...allocations, ...equipmentStats], true, playerBattleModifiers(lockedBase, proficiency));
		manual.addModifier(passive((attribute: EAttribute) => manual.getValue(attribute)));

		expect(composed.getValue(EAttribute.Strength)).toBe(manual.getValue(EAttribute.Strength));
	});
});
