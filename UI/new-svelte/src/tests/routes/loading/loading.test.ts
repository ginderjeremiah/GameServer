import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import LoadingPage from '../../../routes/loading/+page.svelte';

// Mock SvelteKit runtime modules
vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$lib/common', () => ({
	routeTo: vi.fn()
}));
vi.mock('$lib/api', () => ({
	ApiRequest: {
		get: vi.fn().mockResolvedValue([])
	}
}));
vi.mock('$stores', () => ({
	staticData: {
		zones: undefined,
		enemies: undefined,
		items: undefined,
		skills: undefined,
		itemMods: undefined,
		attributes: undefined,
		challenges: undefined
	}
}));

afterEach(cleanup);

describe('Loading page', () => {
	it('renders loading screen container', () => {
		render(LoadingPage);
		expect(screen.getByTestId('loading-screen')).toBeTruthy();
	});

	it('renders heading', () => {
		render(LoadingPage);
		const heading = screen.getByTestId('loading-heading');
		expect(heading).toBeTruthy();
		// Initial state is "checking"
		expect(heading.textContent).toBe('Checking for updates.');
	});

	it('renders progress bar', () => {
		render(LoadingPage);
		expect(screen.getByTestId('progress-bar')).toBeTruthy();
	});

	it('renders manifest window', () => {
		render(LoadingPage);
		expect(screen.getByTestId('manifest-window')).toBeTruthy();
	});

	it('renders enter button in disabled state initially', () => {
		render(LoadingPage);
		const btn = screen.getByTestId('enter-button') as HTMLButtonElement;
		expect(btn).toBeTruthy();
		expect(btn.disabled).toBe(true);
		expect(btn.textContent).toMatch(/Loading/);
	});
});
