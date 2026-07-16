import { dangerModal, staticData } from '$stores';
import { computeReferences, formatReferenceBody, type ReferenceSources } from './references';

/**
 * Builds a {@link ReferenceSources} snapshot from the cached last-saved catalogues in `staticData`
 * — the one place both retire-confirm call sites (the generic Workbench detail pane and the
 * progression editor) assemble the full set, so a new reference-carrying catalogue only needs
 * adding here. `overrides` lets a caller substitute its own live (unsaved-edits-included) records
 * for the one catalogue it's currently editing, since `staticData` only ever reflects last-saved
 * state — every other catalogue still comes from `staticData` as usual.
 */
export function referenceSourcesFromStatic(overrides: Partial<ReferenceSources> = {}): ReferenceSources {
	return {
		enemies: staticData.enemies ?? [],
		zones: staticData.zones ?? [],
		challenges: staticData.challenges ?? [],
		items: staticData.items ?? [],
		itemMods: staticData.itemMods ?? [],
		classes: staticData.classes ?? [],
		skillRecipes: staticData.skillRecipes ?? [],
		proficiencies: staticData.proficiencies ?? [],
		skills: staticData.skills ?? [],
		...overrides
	};
}

/**
 * Reference-source slots that a generic Workbench entity's own reference function reads (`enemies`
 * reads its own `spawns`; `skills` names a referencing recipe via its own catalogue) — see
 * `references.ts`'s `enemyReferences`/`skillReferences`. Every other generic-Workbench entity's
 * reference function only reads *sibling* catalogues it has no live copy of, so overriding those
 * slots would be a no-op (#1976's remaining, genuinely cross-catalogue gap — closing it generically
 * would mean holding every reference-carrying entity's store alive across a tool switch, reversing
 * the documented one-surface-mounted-at-a-time model in `dirty.svelte.ts`).
 */
const SELF_REFERENCING_KEYS = new Set(['enemies', 'skills']);

/**
 * Rebuilds a zero-based-id-indexed array from a store's live `items`, honouring the Id-as-index
 * invariant `references.ts`'s self-referential lookups rely on (`docs/backend.md` → _Reference
 * Data_). `EntityStore.addItem` prepends new records with negative ids, shifting every saved
 * record's array position, so indexing the live array directly would resolve the wrong record. A
 * never-saved (negative-id) record is dropped: only an already-saved record can be a retire target
 * or a referencing recipe's result, so none is looked up by id here.
 */
function denseByLiveId<T extends { id: number }>(live: T[]): T[] {
	const dense: T[] = [];
	for (const record of live) {
		if (record.id >= 0) {
			dense[record.id] = record;
		}
	}
	return dense;
}

/**
 * Live override for the generic Workbench entity currently open, closing the self-referential half
 * of #1976 ("this session's unsaved edits" of the entity's *own* catalogue) for the two entity types
 * whose reference lookup reads it. A no-op for every other entity. Mirrors the progression editor's
 * `{ proficiencies: store.profs }` override, but density-safe since enemies/skills are looked up by
 * array index rather than `proficiencies`' always-`.filter()`-based lookups.
 */
export function ownCatalogueOverride(entityKey: string, items: { id: number }[]): Partial<ReferenceSources> {
	return SELF_REFERENCING_KEYS.has(entityKey)
		? ({ [entityKey]: denseByLiveId(items) } as unknown as Partial<ReferenceSources>)
		: {};
}

interface ConfirmMutationOptions {
	entityKey: string;
	id: number;
	/** Display name used in the confirm dialog body. */
	name: string;
	/** Confirm dialog title, e.g. "Retire Item?". */
	title: string;
	sources: ReferenceSources;
	onConfirmed: () => void;
}

/**
 * Shared flow behind {@link retireWithConfirm} and {@link deleteWithConfirm}: compute the
 * referenced-by surface for a record, confirm via a danger modal only when something actually
 * references it, then run the action.
 */
async function confirmMutation(options: ConfirmMutationOptions, confirmLabel: string): Promise<void> {
	const { entityKey, id, name, title, sources, onConfirmed } = options;
	const groups = computeReferences(entityKey, id, sources);
	if (groups.length > 0) {
		const confirmed = await dangerModal({ title, body: formatReferenceBody(entityKey, name, groups), confirmLabel });
		if (!confirmed) {
			return;
		}
	}
	onConfirmed();
}

/**
 * Used by both the generic Workbench detail pane and the bespoke progression editor
 * (paths/proficiencies aren't a generic `EntityConfig`, so they can't go through
 * {@link ../components/WorkbenchDetail.svelte}).
 */
export function retireWithConfirm(options: ConfirmMutationOptions): Promise<void> {
	return confirmMutation(options, 'Retire anyway');
}

/** Used by the Workbench detail pane for tags — the one non-retireable entity, hard-deleted instead. */
export function deleteWithConfirm(options: ConfirmMutationOptions): Promise<void> {
	return confirmMutation(options, 'Delete anyway');
}
