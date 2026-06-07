import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import BootSplash from '../../routes/BootSplash.svelte';

afterEach(cleanup);

describe('BootSplash', () => {
	it('renders a live status region with an accessible label', () => {
		render(BootSplash);

		const splash = screen.getByTestId('boot-splash');
		expect(splash).toBeTruthy();
		expect(splash.getAttribute('role')).toBe('status');
		expect(screen.getByText(/Restoring your session/i)).toBeTruthy();
	});
});
