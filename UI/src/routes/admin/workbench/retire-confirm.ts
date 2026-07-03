import { dangerModal } from '$stores';
import { computeReferences, formatReferenceBody, type ReferenceSources } from './references';

/**
 * Shared retire flow: compute the referenced-by surface for a record, confirm via a danger modal
 * only when something actually references it, then run the retire action. Used by both the
 * generic Workbench detail pane and the bespoke progression editor (paths/proficiencies aren't a
 * generic `EntityConfig`, so they can't go through {@link ../components/WorkbenchDetail.svelte}).
 */
export async function retireWithConfirm(options: {
	entityKey: string;
	id: number;
	/** Display name used in the confirm dialog body. */
	name: string;
	/** Confirm dialog title, e.g. "Retire Item?". */
	title: string;
	sources: ReferenceSources;
	onConfirmed: () => void;
}): Promise<void> {
	const { entityKey, id, name, title, sources, onConfirmed } = options;
	const groups = computeReferences(entityKey, id, sources);
	if (groups.length > 0) {
		const confirmed = await dangerModal({
			title,
			body: formatReferenceBody(entityKey, name, groups),
			confirmLabel: 'Retire anyway'
		});
		if (!confirmed) {
			return;
		}
	}
	onConfirmed();
}
