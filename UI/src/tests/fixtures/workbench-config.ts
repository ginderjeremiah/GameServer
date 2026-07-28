import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

/* Shared `EntityConfig` test builder (#2481), following the `fixtures/items.ts` convention.

   Unlike the other modules here, `EntityConfig` is an internal workbench contract rather than a
   reference-data one, and its hand-rolled copies were all asserted `as unknown as EntityConfig<T>` —
   so none of them saw a new required field. The double cast was load-bearing for exactly one reason:
   the literals returned `persist: async () => []` instead of the `{ records, idMap }` shape. Spelling
   that shape out is enough to drop both casts, so this builder is genuinely typed and a new required
   member is one edit here rather than a dozen silent ones.

   `newItem` is required rather than defaulted: it is the one member the builder cannot neutrally
   default for an unknown `T` without casting a bare `{ id }` back to it — which would re-open the hole
   this module closes. Every call site already states its blank record, so nothing is lost. */

/**
 * Builds an {@link EntityConfig} for workbench tests. The display fields default to neutral
 * placeholders and the lifecycle members to no-ops, so a suite states only its blank record plus
 * whatever it asserts on.
 *
 * State a value at the call site when the suite asserts on it (a `label` read back as the list header,
 * a `blankName` rendered as the placeholder) even where it matches a default — the assertion should be
 * readable next to the config it reads from.
 */
export const makeEntityConfig = <T extends Identified>(
	overrides: Partial<EntityConfig<T>> & Pick<EntityConfig<T>, 'newItem'>
): EntityConfig<T> => ({
	key: 'rows',
	label: 'Rows',
	singular: 'Row',
	glyph: 'box',
	blankName: 'Unnamed',
	meta: () => [],
	sections: [],
	refresh: async () => [],
	persist: async () => ({ records: [], idMap: new Map() }),
	...overrides
});
