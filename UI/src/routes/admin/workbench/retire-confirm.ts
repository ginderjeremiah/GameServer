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
		paths: staticData.paths ?? [],
		...overrides
	};
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
