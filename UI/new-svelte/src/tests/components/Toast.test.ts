// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, fireEvent, cleanup } from '@testing-library/svelte';
import Toast from '$components/Toast.svelte';

afterEach(cleanup);

describe('Toast', () => {
	it('renders the message', () => {
		const { getByText } = render(Toast, { props: { message: 'Something went wrong' } });
		expect(getByText('Something went wrong')).toBeTruthy();
	});

	it('exposes the toast via the alert role', () => {
		const { getByRole } = render(Toast, { props: { message: 'Heads up' } });
		expect(getByRole('alert')).toBeTruthy();
	});

	it('applies the type as a class for theming', () => {
		const { getByRole } = render(Toast, { props: { message: 'Boom', type: 'error' } });
		expect(getByRole('alert').classList.contains('error')).toBe(true);
	});

	it('fires onDismiss when the dismiss button is clicked', async () => {
		const onDismiss = vi.fn();
		const { getByLabelText } = render(Toast, { props: { message: 'Close me', onDismiss } });

		await fireEvent.click(getByLabelText('Dismiss notification'));
		expect(onDismiss).toHaveBeenCalledTimes(1);
	});

	it('hides the dismiss button when not dismissible', () => {
		const { queryByLabelText } = render(Toast, {
			props: { message: 'No close', dismissible: false }
		});
		expect(queryByLabelText('Dismiss notification')).toBeNull();
	});
});
