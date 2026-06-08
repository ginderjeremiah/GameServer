/**
 * Global modal/dialog store — the single entry point for blocking confirmation dialogs, mirroring
 * the toast store. Anywhere in the app can `await confirmModal({ ... })` and branch on the boolean
 * result; the `ModalHost` mounted in the root layout renders the active dialog and resolves the
 * promise when the user answers (or dismisses via the backdrop / Escape).
 *
 * Requests are queued so two callers that open a modal at once each get their own answer rather than
 * one silently clobbering the other — modals are blocking, so only the head of the queue is shown.
 */

export type ModalKind = 'confirm' | 'acknowledge' | 'destructive';

export interface ModalData {
	id: number;
	title: string;
	body: string;
	kind: ModalKind;
	confirmLabel: string;
	/** Label for the dismissing action. Unused by `acknowledge`, which has a single button. */
	cancelLabel: string;
}

export interface ModalOptions {
	title: string;
	body: string;
	kind?: ModalKind;
	confirmLabel?: string;
	cancelLabel?: string;
}

interface ModalEntry {
	data: ModalData;
	resolve: (confirmed: boolean) => void;
}

const defaultConfirmLabel = (kind: ModalKind): string => (kind === 'acknowledge' ? 'Continue' : 'Confirm');

let nextId = 1;
const queue = $state<ModalEntry[]>([]);

/** The dialog currently shown (the head of the queue), or `null` when none is open. */
export const activeModal = {
	get current(): ModalData | null {
		return queue[0]?.data ?? null;
	}
};

const settle = (id: number, confirmed: boolean) => {
	const index = queue.findIndex((entry) => entry.data.id === id);
	if (index === -1) {
		return;
	}
	const [entry] = queue.splice(index, 1);
	entry.resolve(confirmed);
};

/**
 * Opens a modal and resolves to `true` when the user confirms it and `false` when they dismiss it
 * (cancel button, backdrop click or Escape). An `acknowledge` modal has no cancel path, so it always
 * resolves to `true`.
 */
export const showModal = (options: ModalOptions): Promise<boolean> => {
	const { title, body, kind = 'confirm', confirmLabel, cancelLabel } = options;
	const data: ModalData = {
		id: nextId++,
		title,
		body,
		kind,
		confirmLabel: confirmLabel ?? defaultConfirmLabel(kind),
		cancelLabel: cancelLabel ?? 'Cancel'
	};
	return new Promise<boolean>((resolve) => {
		queue.push({ data, resolve });
	});
};

/** Resolves the active modal as confirmed. */
export const confirmActiveModal = () => {
	const current = activeModal.current;
	if (current) {
		settle(current.id, true);
	}
};

/** Resolves the active modal as cancelled. */
export const cancelActiveModal = () => {
	const current = activeModal.current;
	if (current) {
		settle(current.id, false);
	}
};

/**
 * Dismisses the active modal via a non-button path (backdrop click or Escape). Treated as a cancel
 * for `confirm` / `destructive`, but as an acknowledgement for `acknowledge`, which has no cancel.
 */
export const dismissActiveModal = () => {
	const current = activeModal.current;
	if (current) {
		settle(current.id, current.kind === 'acknowledge');
	}
};

/** Clears every queued modal, resolving each as cancelled. Primarily useful for navigation resets. */
export const clearModals = () => {
	while (queue.length > 0) {
		const [entry] = queue.splice(0, 1);
		entry.resolve(false);
	}
};

/** Convenience helper for a two-action confirm dialog. */
export const confirmModal = (options: Omit<ModalOptions, 'kind'>) => showModal({ ...options, kind: 'confirm' });

/** Convenience helper for a single-button acknowledgement dialog. */
export const acknowledgeModal = (options: Omit<ModalOptions, 'kind'>) => showModal({ ...options, kind: 'acknowledge' });

/** Convenience helper for a destructive confirm dialog (danger-toned primary action). */
export const dangerModal = (options: Omit<ModalOptions, 'kind'>) => showModal({ ...options, kind: 'destructive' });
