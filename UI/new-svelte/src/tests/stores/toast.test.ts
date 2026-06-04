import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
	toasts,
	showToast,
	dismissToast,
	clearToasts,
	toastError,
	toastSuccess,
	toastWarning,
	toastInfo
} from '$stores/toast.svelte';

const current = () => [...toasts.data];

beforeEach(() => {
	vi.useFakeTimers();
	clearToasts();
});

afterEach(() => {
	clearToasts();
	vi.useRealTimers();
});

describe('toast store', () => {
	it('adds a toast with the given message and defaults', () => {
		const id = showToast('Hello');
		const [toast] = current();

		expect(current()).toHaveLength(1);
		expect(toast.id).toBe(id);
		expect(toast.message).toBe('Hello');
		expect(toast.type).toBe('info');
		expect(toast.dismissible).toBe(true);
	});

	it('assigns a unique, increasing id to each toast', () => {
		const first = showToast('a');
		const second = showToast('b');
		expect(second).toBeGreaterThan(first);
		expect(current().map((t) => t.id)).toEqual([first, second]);
	});

	it('auto-dismisses after the given duration', () => {
		showToast('temporary', { duration: 1000 });
		expect(current()).toHaveLength(1);

		vi.advanceTimersByTime(999);
		expect(current()).toHaveLength(1);

		vi.advanceTimersByTime(1);
		expect(current()).toHaveLength(0);
	});

	it('keeps a toast indefinitely when duration is 0', () => {
		showToast('sticky', { duration: 0 });
		vi.advanceTimersByTime(60_000);
		expect(current()).toHaveLength(1);
	});

	it('collapses duplicate messages of the same type and refreshes the timer', () => {
		const first = showToast('Connection error', { type: 'error', duration: 1000 });
		vi.advanceTimersByTime(800);

		const second = showToast('Connection error', { type: 'error', duration: 1000 });
		expect(second).toBe(first);
		expect(current()).toHaveLength(1);

		// Timer was refreshed by the duplicate, so the original deadline passes
		// without dismissing it.
		vi.advanceTimersByTime(800);
		expect(current()).toHaveLength(1);

		vi.advanceTimersByTime(200);
		expect(current()).toHaveLength(0);
	});

	it('does not collapse identical messages of different types', () => {
		showToast('Same text', { type: 'error' });
		showToast('Same text', { type: 'success' });
		expect(current()).toHaveLength(2);
	});

	it('dismissToast removes a specific toast and clears its timer', () => {
		const id = showToast('to remove', { duration: 1000 });
		dismissToast(id);
		expect(current()).toHaveLength(0);

		// No timer should fire after manual dismissal.
		vi.advanceTimersByTime(1000);
		expect(current()).toHaveLength(0);
	});

	it('clearToasts removes every toast', () => {
		showToast('a');
		showToast('b');
		clearToasts();
		expect(current()).toHaveLength(0);
	});

	it('convenience helpers set the matching type', () => {
		toastError('err');
		toastSuccess('ok');
		toastWarning('warn');
		toastInfo('info');
		expect(current().map((t) => t.type)).toEqual(['error', 'success', 'warning', 'info']);
	});
});
