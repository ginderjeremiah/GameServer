import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
	activeModal,
	showModal,
	confirmActiveModal,
	cancelActiveModal,
	dismissActiveModal,
	clearModals,
	confirmModal,
	acknowledgeModal,
	dangerModal
} from '$stores/modal.svelte';

beforeEach(() => clearModals());
afterEach(() => clearModals());

describe('modal store', () => {
	it('shows the requested modal as the active one with sensible defaults', () => {
		void showModal({ title: 'Title', body: 'Body' });
		const active = activeModal.current;

		expect(active).not.toBeNull();
		expect(active?.title).toBe('Title');
		expect(active?.body).toBe('Body');
		expect(active?.kind).toBe('confirm');
		expect(active?.confirmLabel).toBe('Confirm');
		expect(active?.cancelLabel).toBe('Cancel');
	});

	it('defaults an acknowledge modal primary label to Continue', () => {
		void acknowledgeModal({ title: 'Done', body: 'All set' });
		expect(activeModal.current?.kind).toBe('acknowledge');
		expect(activeModal.current?.confirmLabel).toBe('Continue');
	});

	it('honours explicit labels and kind', () => {
		void dangerModal({
			title: 'Reset?',
			body: 'Cannot be undone',
			confirmLabel: 'Reset',
			cancelLabel: 'Keep'
		});
		expect(activeModal.current?.kind).toBe('destructive');
		expect(activeModal.current?.confirmLabel).toBe('Reset');
		expect(activeModal.current?.cancelLabel).toBe('Keep');
	});

	it('resolves true when confirmed and clears the active modal', async () => {
		const result = confirmModal({ title: 'T', body: 'B' });
		confirmActiveModal();
		await expect(result).resolves.toBe(true);
		expect(activeModal.current).toBeNull();
	});

	it('resolves false when cancelled', async () => {
		const result = confirmModal({ title: 'T', body: 'B' });
		cancelActiveModal();
		await expect(result).resolves.toBe(false);
		expect(activeModal.current).toBeNull();
	});

	it('treats a backdrop/Escape dismissal as a cancel for confirm modals', async () => {
		const result = confirmModal({ title: 'T', body: 'B' });
		dismissActiveModal();
		await expect(result).resolves.toBe(false);
	});

	it('treats a dismissal as an acknowledgement for acknowledge modals', async () => {
		const result = acknowledgeModal({ title: 'T', body: 'B' });
		dismissActiveModal();
		await expect(result).resolves.toBe(true);
	});

	it('queues concurrent modals and shows them one at a time', async () => {
		const first = showModal({ title: 'First', body: 'B' });
		const second = showModal({ title: 'Second', body: 'B' });

		// Only the head of the queue is active.
		expect(activeModal.current?.title).toBe('First');

		confirmActiveModal();
		await expect(first).resolves.toBe(true);

		// The next queued modal becomes active.
		expect(activeModal.current?.title).toBe('Second');
		cancelActiveModal();
		await expect(second).resolves.toBe(false);
		expect(activeModal.current).toBeNull();
	});

	it('clearModals resolves every queued modal as cancelled', async () => {
		const first = confirmModal({ title: 'First', body: 'B' });
		const second = confirmModal({ title: 'Second', body: 'B' });

		clearModals();

		await expect(first).resolves.toBe(false);
		await expect(second).resolves.toBe(false);
		expect(activeModal.current).toBeNull();
	});

	it('is a no-op to confirm/cancel when no modal is open', () => {
		expect(() => confirmActiveModal()).not.toThrow();
		expect(() => cancelActiveModal()).not.toThrow();
		expect(activeModal.current).toBeNull();
	});
});
