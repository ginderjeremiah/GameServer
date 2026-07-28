import { EAttribute, EAttributeType, type IAttribute } from '$lib/api';

/* Shared `IAttribute` contract builder. Excluded on purpose: the Codex suite's untyped `staticData`
   mock (`{} as any`). See `docs/frontend.md` → Testing Guidelines for the contract-drift rule. */

/**
 * Builds an {@link IAttribute} reference-data entry for tests. The display metadata defaults to
 * neutral placeholders so tests that only care about id/name resolution stay terse; pass `overrides`
 * to set any field that a test actually asserts on.
 */
export const makeAttribute = (id: EAttribute, name: string, overrides: Partial<IAttribute> = {}): IAttribute => ({
	id,
	name,
	description: '',
	attributeType: EAttributeType.Primary,
	isPercentage: false,
	isHarmful: false,
	code: '',
	displayOrder: 0,
	decimals: 0,
	...overrides
});
