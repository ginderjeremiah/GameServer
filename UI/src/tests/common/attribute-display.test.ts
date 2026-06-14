import { describe, it, expect } from 'vitest';
import { EAttribute, type IAttribute } from '$lib/api';
import { attributeEnumName, attributeName } from '../../lib/common/attribute-display';
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
