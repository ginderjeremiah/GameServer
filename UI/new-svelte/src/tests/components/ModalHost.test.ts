// @vitest-environment jsdom
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { render, fireEvent, cleanup, waitFor } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import ModalHost from '$components/ModalHost.svelte';
import { confirmModal, acknowledgeModal, clearModals, activeModal } from '$stores/modal.svelte';

beforeEach(() => clearModals());

afterEach(() => {
	cleanup();
	clearModals();
});

describe('ModalHost', () => {
	it('renders nothing when no modal is active', () => {
		const { queryByRole } = render(ModalHost);
		expect(queryByRole('dialog')).toBeNull();
	});

	it('renders the active modal and resolves true when confirmed', async () => {
		const { getByText, queryByRole } = render(ModalHost);
		const result = confirmModal({ title: 'Proceed?', body: 'Body', confirmLabel: 'Go' });
		flushSync();

		expect(queryByRole('dialog')).not.toBeNull();
		await fireEvent.click(getByText('Go'));
		flushSync();

		await expect(result).resolves.toBe(true);
		expect(queryByRole('dialog')).toBeNull();
	});

	it('resolves false when the cancel button is clicked', async () => {
		const { getByText } = render(ModalHost);
		const result = confirmModal({ title: 'Proceed?', body: 'Body', cancelLabel: 'Nope' });
		flushSync();

		await fireEvent.click(getByText('Nope'));
		flushSync();
		await expect(result).resolves.toBe(false);
	});

	it('cancels the active modal when the backdrop is clicked', async () => {
		const { getByLabelText } = render(ModalHost);
		const result = confirmModal({ title: 'Proceed?', body: 'Body' });
		flushSync();

		await fireEvent.click(getByLabelText('Dismiss dialog'));
		flushSync();
		await expect(result).resolves.toBe(false);
	});

	it('cancels the active modal when Escape is pressed', async () => {
		render(ModalHost);
		const result = confirmModal({ title: 'Proceed?', body: 'Body' });
		flushSync();

		await fireEvent.keyDown(window, { key: 'Escape' });
		flushSync();
		await expect(result).resolves.toBe(false);
	});

	it('treats Escape as an acknowledgement for an acknowledge modal', async () => {
		render(ModalHost);
		const result = acknowledgeModal({ title: 'Done', body: 'Body' });
		flushSync();

		await fireEvent.keyDown(window, { key: 'Escape' });
		flushSync();
		await expect(result).resolves.toBe(true);
	});

	it('locks body scroll while a modal is open and restores it on close', async () => {
		const { getByText } = render(ModalHost);
		const result = confirmModal({ title: 'Proceed?', body: 'Body', confirmLabel: 'Go' });
		flushSync();

		expect(document.body.classList.contains('modal-open')).toBe(true);

		await fireEvent.click(getByText('Go'));
		flushSync();
		await result;
		await waitFor(() => expect(document.body.classList.contains('modal-open')).toBe(false));
	});

	it('shows queued modals one at a time', async () => {
		const { getByText, queryByText } = render(ModalHost);
		const first = confirmModal({ title: 'First', body: 'Body', confirmLabel: 'OkOne' });
		confirmModal({ title: 'Second', body: 'Body', confirmLabel: 'OkTwo' });
		flushSync();

		expect(getByText('First')).toBeTruthy();
		expect(queryByText('Second')).toBeNull();

		await fireEvent.click(getByText('OkOne'));
		flushSync();
		await first;

		expect(activeModal.current?.title).toBe('Second');
	});
});
