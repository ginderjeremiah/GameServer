import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { SERVER_STAT_TYPES } from './stat-fixtures';

// EntityPicker reads its entity lists through a StatisticsView, which resolves
// them from the in-memory staticData store — mocked here.
const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ staticData }));

import EntityPicker from '$routes/game/screens/stats/EntityPicker.svelte';
import { StatisticsView } from '$routes/game/screens/stats/statistics-view.svelte';

beforeEach(() => {
	staticData.statisticTypes = SERVER_STAT_TYPES;
	staticData.enemies = [
		{ id: 0, name: 'Cave Bat', isBoss: false },
		{ id: 1, name: 'Goblin', isBoss: true }
	];
	staticData.zones = [{ id: 0, name: 'Verdant Hollow', order: 3 }];
	staticData.skills = [{ id: 0, name: 'Cleave' }];
});

afterEach(() => cleanup());

describe('EntityPicker', () => {
	it('lists the entities of the active kind and tags a boss enemy', () => {
		render(EntityPicker, { props: { view: new StatisticsView() } });

		expect(screen.getByText('Cave Bat')).toBeTruthy();
		expect(screen.getByText('Goblin')).toBeTruthy();
		// The boss enemy carries a "Boss" sub-label.
		expect(screen.getByText('Boss')).toBeTruthy();
	});

	it('filters the list by the search query', async () => {
		render(EntityPicker, { props: { view: new StatisticsView() } });

		await fireEvent.input(screen.getByLabelText('Search enemies'), { target: { value: 'gob' } });

		expect(screen.queryByText('Cave Bat')).toBeNull();
		expect(screen.getByText('Goblin')).toBeTruthy();
	});

	it('shows an empty state when nothing matches', async () => {
		render(EntityPicker, { props: { view: new StatisticsView() } });

		await fireEvent.input(screen.getByLabelText('Search enemies'), { target: { value: 'zzzzz' } });

		expect(screen.getByText('No matches.')).toBeTruthy();
	});

	it('selects an entity on click, marking its button active', async () => {
		const view = new StatisticsView();
		render(EntityPicker, { props: { view } });

		await fireEvent.click(screen.getByTestId('entity-1'));

		expect(view.entId).toBe(1);
		expect(screen.getByTestId('entity-1').classList.contains('on')).toBe(true);
	});

	it('renders the zone order sub-label for zone entities', () => {
		const view = new StatisticsView();
		view.switchKind('zone');
		render(EntityPicker, { props: { view } });

		expect(screen.getByText('Verdant Hollow')).toBeTruthy();
		expect(screen.getByText('Z3')).toBeTruthy();
	});
});
