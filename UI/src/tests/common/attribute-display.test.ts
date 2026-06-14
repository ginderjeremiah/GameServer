import { describe, it, expect } from 'vitest';
import { EAttribute, type IAttribute } from '$lib/api';
import {
	attributeCode,
	attributeEnumName,
	attributeIsHarmful,
	attributeName
} from '../../lib/common/attribute-display';
import { makeAttribute } from '../fixtures/attributes';

describe('attributeEnumName', () => {
	it('returns the normalized enum key for a known attribute id', () => {
		expect(attributeEnumName(EAttribute.MaxHealth)).toBe('Max Health');
	});

	it('returns the single-word name for a single-word attribute', () => {
		expect(attributeEnumName(EAttribute.Strength)).toBe('Strength');
	});

	it('degrades to "Unknown" for an out-of-range id rather than throwing', () => {
		// `EAttribute[id]` is `undefined` for an unknown id; the fallback must not crash a
		// hot display path (#493) and should render a readable label, not a blank string.
		const unknownId = 999 as EAttribute;
		expect(() => attributeEnumName(unknownId)).not.toThrow();
		expect(attributeEnumName(unknownId)).toBe('Unknown');
	});
});

describe('attributeName', () => {
	const mockAttributes: IAttribute[] = [
		makeAttribute(EAttribute.Strength, 'Physical Power'),
		makeAttribute(EAttribute.MaxHealth, 'Maximum Life')
	];

	it('returns the reference-data name when attributes are provided', () => {
		expect(attributeName(EAttribute.Strength, mockAttributes)).toBe('Physical Power');
		expect(attributeName(EAttribute.MaxHealth, mockAttributes)).toBe('Maximum Life');
	});

	it('falls back to the normalised enum name when attributes are absent', () => {
		expect(attributeName(EAttribute.Strength)).toBe('Strength');
		expect(attributeName(EAttribute.MaxHealth)).toBe('Max Health');
		expect(attributeName(EAttribute.CooldownRecovery)).toBe('Cooldown Recovery');
	});

	it('falls back to the normalised enum name when the id is absent from the provided list', () => {
		expect(attributeName(EAttribute.Luck, mockAttributes)).toBe('Luck');
	});

	it('degrades to "Unknown" for an out-of-range id rather than throwing', () => {
		const unknownId = 999 as EAttribute;
		expect(() => attributeName(unknownId)).not.toThrow();
		expect(attributeName(unknownId)).toBe('Unknown');
	});
});

describe('attributeCode', () => {
	const mockAttributes: IAttribute[] = [
		makeAttribute(EAttribute.Strength, 'Strength', { code: 'STR' }),
		makeAttribute(EAttribute.MaxHealth, 'Max Health', { code: '' })
	];

	it('returns the reference-data code when attributes are provided', () => {
		expect(attributeCode(EAttribute.Strength, mockAttributes)).toBe('STR');
	});

	it('returns the empty code verbatim for an attribute the backend assigns none', () => {
		expect(attributeCode(EAttribute.MaxHealth, mockAttributes)).toBe('');
	});

	it('falls back to the normalised enum name when attributes are absent', () => {
		expect(attributeCode(EAttribute.Strength)).toBe('Strength');
		expect(attributeCode(EAttribute.MaxHealth)).toBe('Max Health');
	});

	it('falls back to the normalised enum name when the id is absent from the provided list', () => {
		expect(attributeCode(EAttribute.Luck, mockAttributes)).toBe('Luck');
	});
});

describe('attributeIsHarmful', () => {
	const mockAttributes: IAttribute[] = [
		makeAttribute(EAttribute.DamageTakenPerSecond, 'Damage Taken Per Second', { isHarmful: true }),
		makeAttribute(EAttribute.Strength, 'Strength', { isHarmful: false })
	];

	it('reads the harmful flag from the reference set', () => {
		expect(attributeIsHarmful(EAttribute.DamageTakenPerSecond, mockAttributes)).toBe(true);
		expect(attributeIsHarmful(EAttribute.Strength, mockAttributes)).toBe(false);
	});

	it('defaults to false when attributes are absent or the id is unknown', () => {
		expect(attributeIsHarmful(EAttribute.DamageTakenPerSecond)).toBe(false);
		expect(attributeIsHarmful(EAttribute.Luck, mockAttributes)).toBe(false);
	});
});
