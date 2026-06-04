// @vitest-environment jsdom
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
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

	it('removes a toast from the DOM when its dismiss button is clicked', async () => {
		const { getByLabelText, queryByText } = render(ToastContainer);
		showToast('Dismiss me', { duration: 0 });
		flushSync();

		await fireEvent.click(getByLabelText('Dismiss notification'));
		flushSync();

		expect(queryByText('Dismiss me')).toBeNull();
		expect([...toasts.data]).toHaveLength(0);
	});
});
