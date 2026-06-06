import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import Statistics from '$routes/game/screens/stats/Statistics.svelte';

// Statistics builds its view-model from the self-contained mock data source, so
// no managers/stores need stubbing here.

afterEach(() => cleanup());

describe('Statistics screen', () => {
	it('renders the screen with the by-statistic view by default', () => {
		render(Statistics);
		expect(screen.getByTestId('statistics-screen')).toBeTruthy();
		expect(screen.getByText('Statistics')).toBeTruthy();
		// The Combat category is active by default → its stat cards are shown.
		expect(screen.getByText('Enemies Killed')).toBeTruthy();
		expect(screen.getByTestId('stat-card-grid')).toBeTruthy();
	});

	it('switches to the by-entity view via the top toggle', async () => {
		render(Statistics);
		expect(screen.queryByTestId('entity-picker')).toBeNull();
		await fireEvent.click(screen.getByTestId('view-entity'));
		expect(screen.getByTestId('entity-picker')).toBeTruthy();
		expect(screen.getByTestId('entity-dossier')).toBeTruthy();
	});

	it('switches category tabs to show that category’s statistics', async () => {
		render(Statistics);
		await fireEvent.click(screen.getByTestId('tab-time'));
		expect(screen.getByText('Total Battle Time')).toBeTruthy();
		expect(screen.getByText('Fastest Victory')).toBeTruthy();
	});

	it('pivots from a stat card entity row into that entity’s dossier (cross-link)', async () => {
		render(Statistics);
		// Cave Bat (entity 0) is the most-killed enemy, so its row is in the
		// Enemies Killed card (stat id 1). Clicking it pivots to the dossier.
		await fireEvent.click(screen.getByTestId('stat-row-1-0'));
		expect(screen.getByTestId('entity-dossier')).toBeTruthy();
		expect(screen.getByTestId('entity-picker')).toBeTruthy();
	});
});
