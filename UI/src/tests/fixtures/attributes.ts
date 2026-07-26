import { EAttribute, EAttributeType, type IAttribute } from '$lib/api';

/* A literal asserted `as IAttribute` claims the contract to its consumer while silencing a missing
   required field, so it looks covered and isn't. #2447 folded those in (`battle/battle-attributes`,
   `skills/SkillDamageBreakdown`, `select/class-summary`); reintroducing such a cast re-opens the hole,
   so state why if you do. */

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
