import { describe, it, expect } from 'vitest';
import { EAttribute } from '$lib/api';
import { attributeEnumName } from '../../lib/common/attribute-display';

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
