import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import LoadingHeader from '$routes/loading/LoadingHeader.svelte';
import type { LoadingView, LoadItem, Phase } from '$routes/loading/loading-view.svelte';

// LoadingHeader only reads `phase`, `title` and `currentItem` off the view, so a
// lightweight stand-in exercises its markup branches without the view's network boot.
const TITLES: Record<Phase, string> = {
	checking: 'Checking for updates.',
	loading: 'Preparing the realm.',
	error: 'Connection failed.',
	done: 'Ready.'
};

const item = (label: string): LoadItem => ({
	key: label.toLowerCase(),
	label,
	status: 'loading',
	durationMs: 0,
	error: null,
	fetch: async () => 0
});

const stubView = (phase: Phase, currentItem: LoadItem | null = null): LoadingView =>
	({ phase, currentItem, title: TITLES[phase] }) as unknown as LoadingView;

afterEach(cleanup);

describe('LoadingHeader', () => {
	it('shows the checking subtitle in the checking phase', () => {
		render(LoadingHeader, { props: { view: stubView('checking') } });
		expect(screen.getByTestId('loading-heading').textContent).toBe('Checking for updates.');
		expect(screen.getByText('Verifying cached reference data…')).toBeTruthy();
	});

	it('names the current set while loading', () => {
		render(LoadingHeader, { props: { view: stubView('loading', item('Enemies')) } });
		expect(screen.getByText('enemies', { exact: false })).toBeTruthy();
		expect(screen.queryByText('All systems nominal.')).toBeNull();
	});

	it('marks the heading as error and names the failed set', () => {
		render(LoadingHeader, { props: { view: stubView('error', item('Zones')) } });
		const heading = screen.getByTestId('loading-heading');
		expect(heading.classList.contains('error-text')).toBe(true);
		expect(screen.getByText('zones', { exact: false })).toBeTruthy();
	});

	it('shows the all-clear subtitle when done', () => {
		render(LoadingHeader, { props: { view: stubView('done') } });
		expect(screen.getByText('All systems nominal.')).toBeTruthy();
	});
});
