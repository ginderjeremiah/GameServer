import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import ErrorBanner from '$routes/loading/ErrorBanner.svelte';

afterEach(cleanup);

describe('ErrorBanner', () => {
	it('renders the supplied message', () => {
		render(ErrorBanner, { props: { message: 'Network error — could not reach server.', onRetry: vi.fn() } });
		expect(screen.getByText('Network error — could not reach server.')).toBeTruthy();
	});

	it('invokes onRetry when the retry button is clicked', async () => {
		const onRetry = vi.fn();
		render(ErrorBanner, { props: { message: 'boom', onRetry } });

		await fireEvent.click(screen.getByTestId('retry-button'));

		expect(onRetry).toHaveBeenCalledTimes(1);
	});
});
