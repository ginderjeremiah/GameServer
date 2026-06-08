// @vitest-environment jsdom
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, fireEvent, cleanup, waitFor } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import ToastContainer from '$components/ToastContainer.svelte';
import { showToast, clearToasts, toasts } from '$stores/toast.svelte';

beforeEach(() => clearToasts());

afterEach(() => {
	cleanup();
	clearToasts();
});

describe('ToastContainer', () => {
	it('renders a toast added to the store', async () => {
		const { findByText } = render(ToastContainer);
		showToast('Live message', { duration: 0 });
		flushSync();
		expect(await findByText('Live message')).toBeTruthy();
	});

	it('renders multiple toasts', async () => {
		const { findAllByRole } = render(ToastContainer);
		showToast('one', { duration: 0 });
		showToast('two', { duration: 0 });
		flushSync();
		expect(await findAllByRole('alert')).toHaveLength(2);
	});

	it('removes a dismissed toast from the store immediately and from the DOM after its exit animation', async () => {
		const { getByLabelText, queryByText } = render(ToastContainer);
		showToast('Dismiss me', { duration: 0 });
		flushSync();

		await fireEvent.click(getByLabelText('Dismiss notification'));
		flushSync();

		// The store is the synchronous source of truth: the toast is gone from it immediately, but the
		// container keeps the node briefly so it can animate out.
		expect([...toasts.data]).toHaveLength(0);
		await waitFor(() => {
			flushSync();
			expect(queryByText('Dismiss me')).toBeNull();
		});
	});

	it('wires an inline action through to the toast', async () => {
		const onClick = vi.fn();
		const { getByText } = render(ToastContainer);
		showToast('Sync failed', { duration: 0, action: { label: 'Retry', onClick } });
		flushSync();

		await fireEvent.click(getByText('Retry →'));
		flushSync();

		expect(onClick).toHaveBeenCalledTimes(1);
		// Acting on the toast also dismisses it from the store.
		expect([...toasts.data]).toHaveLength(0);
	});
});
