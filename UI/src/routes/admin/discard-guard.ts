import { dangerModal } from '$stores';

/**
 * Prompts to discard pending edits (via a danger modal) when `pending` unsaved changes exist;
 * resolves `true` immediately, without prompting, when there is nothing to lose. Shared by every
 * way the admin shell can leave a dirty Workbench/Progression surface — a sidebar tool switch or
 * "Return to Game" — so the count-based pluralization and confirm copy live in one place.
 */
export async function confirmDiscard(pending: number): Promise<boolean> {
	if (pending === 0) {
		return true;
	}
	return dangerModal({
		title: 'Discard unsaved changes?',
		body: `You have ${pending} unsaved ${pending === 1 ? 'change' : 'changes'}. Leaving now will discard ${pending === 1 ? 'it' : 'them'}.`,
		confirmLabel: 'Discard and continue'
	});
}

/**
 * Blocks a full page unload (refresh, close, external navigation) while `pending` unsaved changes
 * exist. Client-side navigation away from `/admin` (tool switch, "Return to Game") is guarded by
 * {@link confirmDiscard} instead — this is the backstop for the case that skips the SPA router.
 */
export function guardBeforeUnload(event: BeforeUnloadEvent, pending: number): void {
	if (pending > 0) {
		event.preventDefault();
		event.returnValue = '';
	}
}
