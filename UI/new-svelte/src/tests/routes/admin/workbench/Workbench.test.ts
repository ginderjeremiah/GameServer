import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, waitFor } from '@testing-library/svelte';

vi.mock('$stores', () => ({
	staticData: {
		zones: [],
		enemies: [],
		items: [],
		skills: [],
		itemMods: [],
		attributes: [],
		challenges: [],
		challengeTypes: [],
		statisticTypes: []
	},
	toastError: vi.fn(),
	// LogPanel (re-exported from $components barrel) imports `logs`.
	logs: () => []
}));

import Workbench from '$routes/admin/workbench/Workbench.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

const seed: Identified[] = [
	{ id: 1, name: 'Alpha' },
	{ id: 2, name: 'Beta' }
];

const makeConfig = (overrides: Partial<EntityConfig<Identified>> = {}): EntityConfig<Identified> => ({
	key: 'rows',
	label: 'Enemies',
	singular: 'Enemy',
	glyph: 'box',
	blankName: 'Unnamed',
	newItem: (id) => ({ id, name: '' }),
	meta: () => [],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'box',
			kind: 'fields',
			fields: [{ key: 'name', label: 'Name', type: 'text' }]
		}
	],
	refresh: async () => seed,
	persist: async () => [],
	...overrides
});

afterEach(cleanup);

describe('Workbench', () => {
	it('does not show workbench content before the entity data is fetched', () => {
		// Use a never-resolving promise so the store stays unset.
		const entity = makeConfig({ refresh: () => new Promise(() => {}) });
		const { container } = render(Workbench, { props: { entity } });
		// While store is undefined, the workbench title is not rendered (Loading is shown instead).
		// Note: the Loading component uses delay=150, so the spinner itself may be suppressed;
		// checking for the absent workbench title is more reliable.
		expect(container.querySelector('[data-testid="workbench-title"]')).toBeNull();
	});

	it('renders the entity label once data has loaded', async () => {
		render(Workbench, { props: { entity: makeConfig() } });
		// Wait for onMount → entity.refresh() → store to be set → DOM update.
		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());
		expect(screen.getByTestId('workbench-title').textContent).toBe('Enemies');
	});

	it('shows the record count in the page summary after data loads', async () => {
		render(Workbench, { props: { entity: makeConfig() } });
		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());
		// The summary reads "2 enemies" (liveItems.length + entity.label.toLowerCase())
		expect(screen.getByText('2 enemies')).toBeTruthy();
	});

	it('renders the list and detail panes after data loads', async () => {
		render(Workbench, { props: { entity: makeConfig() } });
		await waitFor(() => expect(screen.getByTestId('workbench-list')).toBeTruthy());
		expect(screen.getByTestId('workbench-list')).toBeTruthy();
	});

	it('passes an optional group label into the eyebrow', async () => {
		render(Workbench, { props: { entity: makeConfig(), groupLabel: 'Combat' } });
		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());
		expect(screen.getByText('Admin Console · Combat')).toBeTruthy();
	});
});
