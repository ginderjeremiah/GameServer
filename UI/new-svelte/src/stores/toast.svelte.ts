import { SvelteMap } from 'svelte/reactivity';

export type ToastType = 'error' | 'success' | 'warning' | 'info';

export interface ToastData {
	id: number;
	message: string;
	type: ToastType;
	/** Whether a manual dismiss control is shown for the toast. */
	dismissible: boolean;
	/** Optional callback run once the toast is dismissed (manually or by auto-dismiss). */
	onDismiss?: () => void;
}

export interface ToastOptions {
	type?: ToastType;
	/** Auto-dismiss delay in ms. Pass `0` to keep the toast until it is dismissed manually. */
	duration?: number;
	dismissible?: boolean;
	/**
	 * Callback run once the toast is dismissed — whether by the manual dismiss control or by
	 * auto-dismiss, but not by a bulk `clearToasts` reset. Useful for follow-up actions such as
	 * navigating away once the user has acknowledged the notification.
	 */
	onDismiss?: () => void;
}

const DEFAULT_DURATION = 6000;

// Keyed by a stable id (mirrors the tooltip store) so dismissal is reference- and
// order-independent even while the container is actively rendering the collection.
const toastData = new SvelteMap<number, ToastData>();
// Auto-dismiss timer handles only — never read during rendering, so this map is
// deliberately non-reactive (a SvelteMap here would imply reactivity it doesn't need).
// eslint-disable-next-line svelte/prefer-svelte-reactivity
const timers = new Map<number, ReturnType<typeof setTimeout>>();

let nextId = 1;

export const toasts = {
	get data() {
		return toastData.values();
	}
};

const clearTimer = (id: number) => {
	const timer = timers.get(id);
	if (timer !== undefined) {
		clearTimeout(timer);
		timers.delete(id);
	}
};

const scheduleDismiss = (id: number, duration: number) => {
	clearTimer(id);
	if (duration > 0) {
		timers.set(
			id,
			setTimeout(() => dismissToast(id), duration)
		);
	}
};

export const dismissToast = (id: number) => {
	clearTimer(id);
	const toast = toastData.get(id);
	// Remove before calling back so a re-rendering callback never observes a stale dismissed toast.
	toastData.delete(id);
	toast?.onDismiss?.();
};

/** Removes every active toast. Primarily useful for navigation resets and tests. */
export const clearToasts = () => {
	for (const id of [...timers.keys()]) {
		clearTimer(id);
	}
	toastData.clear();
};

export const showToast = (message: string, options: ToastOptions = {}): number => {
	const { type = 'info', duration = DEFAULT_DURATION, dismissible = true, onDismiss } = options;

	// Collapse a duplicate, still-visible toast (e.g. a socket error that keeps
	// firing) into the existing one and simply refresh its timer rather than
	// stacking identical messages on top of each other.
	for (const toast of toastData.values()) {
		if (toast.message === message && toast.type === type) {
			// Collapsing keeps the existing toast's onDismiss; a differing callback on a duplicate is ignored.
			scheduleDismiss(toast.id, duration);
			return toast.id;
		}
	}

	const id = nextId++;
	toastData.set(id, { id, message, type, dismissible, onDismiss });
	scheduleDismiss(id, duration);
	return id;
};

export const toastError = (message: string, options: Omit<ToastOptions, 'type'> = {}) =>
	showToast(message, { ...options, type: 'error' });

export const toastSuccess = (message: string, options: Omit<ToastOptions, 'type'> = {}) =>
	showToast(message, { ...options, type: 'success' });

export const toastWarning = (message: string, options: Omit<ToastOptions, 'type'> = {}) =>
	showToast(message, { ...options, type: 'warning' });

export const toastInfo = (message: string, options: Omit<ToastOptions, 'type'> = {}) =>
	showToast(message, { ...options, type: 'info' });
