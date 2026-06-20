import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { SERVER_STAT_TYPES } from './stat-fixtures';

// EntityPicker reads its entity lists through a StatisticsView, which resolves them from the
// in-memory staticData store and (for the enemy deep-link) the navigation store — both mocked here.
const { staticData, navigation } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any,
	navigation: { requestScreen: vi.fn() }
}));
vi.mock('$stores', () => ({ staticData, navigation }));

import EntityPicker from '$routes/game/screens/stats/EntityPicker.svelte';
import { StatisticsView } from '$routes/game/screens/stats/statistics-view.svelte';

beforeEach(() => {
	staticData.statisticTypes = SERVER_STAT_TYPES;
	staticData.enemies = [
		{ id: 0, name: 'Cave Bat', isBoss: false },
		{ id: 1, name: 'Goblin', isBoss: true }
	];
	staticData.zones = [
		{ id: 0, name: 'Verdant Hollow', order: 3 },
		{ id: 1, name: 'Frostspire', order: 5 }
	];
	staticData.skills = [{ id: 0, name: 'Cleave' }];
	navigation.requestScreen.mockClear();
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

	it('deep-links an enemy to the Codex dossier instead of selecting it in place', async () => {
		const view = new StatisticsView();
		render(EntityPicker, { props: { view } });

		await fireEvent.click(screen.getByTestId('entity-1'));

		// Enemies now open in the Codex (their per-entity stats live there); the in-place selection
		// is untouched.
		expect(navigation.requestScreen).toHaveBeenCalledWith('codex', { tab: 'enemies', enemyId: 1, sub: 'statistics' });
		expect(view.entId).toBe(0);
	});

	it('selects a non-enemy entity in place, marking its button active', async () => {
		const view = new StatisticsView();
		view.switchKind('zone');
		render(EntityPicker, { props: { view } });

		await fireEvent.click(screen.getByTestId('entity-1'));

		expect(navigation.requestScreen).not.toHaveBeenCalled();
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
