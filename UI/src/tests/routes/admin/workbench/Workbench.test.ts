import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, waitFor, fireEvent } from '@testing-library/svelte';

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
import { workbenchDirty } from '$routes/admin/workbench/dirty.svelte';
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
	persist: async () => ({ records: [], idMap: new Map() }),
	...overrides
});

afterEach(cleanup);

describe('Workbench — unsaved-change reporting', () => {
	it("reports the store's pending-change count to the shared workbenchDirty tracker", async () => {
		render(Workbench, { props: { entity: makeConfig() } });
		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());
		expect(workbenchDirty.total).toBe(0);

		await fireEvent.click(screen.getByTestId('workbench-new'));
		await waitFor(() => expect(workbenchDirty.total).toBe(1));
	});

	it('resets the tracker on unmount so a stale count cannot block navigation elsewhere', async () => {
		const { unmount } = render(Workbench, { props: { entity: makeConfig() } });
		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());
		await fireEvent.click(screen.getByTestId('workbench-new'));
		await waitFor(() => expect(workbenchDirty.total).toBe(1));

		unmount();
		expect(workbenchDirty.total).toBe(0);
	});
});

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

	it('shows an error state with a Refresh action when the seed load fails, instead of spinning forever', async () => {
		const { toastError } = await import('$stores');
		const entity = makeConfig({ refresh: vi.fn(async () => Promise.reject(new Error('socket unreachable'))) });
		render(Workbench, { props: { entity } });

		await waitFor(() => expect(screen.getByTestId('workbench-error')).toBeTruthy());
		expect(screen.getByTestId('workbench-error').textContent).toContain('socket unreachable');
		expect(screen.queryByTestId('workbench-title')).toBeNull();
		expect(toastError).toHaveBeenCalledWith('socket unreachable');
	});

	it('retries the seed load when Refresh is clicked after a failure, and renders normally once it succeeds', async () => {
		const refresh = vi.fn().mockRejectedValueOnce(new Error('socket unreachable')).mockResolvedValueOnce(seed);
		const entity = makeConfig({ refresh });
		render(Workbench, { props: { entity } });

		await waitFor(() => expect(screen.getByTestId('workbench-error')).toBeTruthy());

		await fireEvent.click(screen.getByText('Refresh'));

		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());
		expect(screen.queryByTestId('workbench-error')).toBeNull();
		expect(refresh).toHaveBeenCalledTimes(2);
	});

	it('follows a newly-added record to its persisted id after save, instead of jumping to the first record', async () => {
		const persist = vi.fn(async (diff: { added: Identified[] }) => ({
			records: [...seed, ...diff.added.map((record, i) => ({ ...record, id: 3 + i }))],
			idMap: new Map(diff.added.map((record, i) => [record.id, 3 + i]))
		}));
		render(Workbench, { props: { entity: makeConfig({ persist }) } });
		await waitFor(() => expect(screen.getByTestId('workbench-title')).toBeTruthy());

		await fireEvent.click(screen.getByTestId('workbench-new'));
		const selectedBeforeSave = screen.getAllByTestId('workbench-row').find((row) => row.classList.contains('selected'));
		expect(selectedBeforeSave?.textContent).toContain('Unnamed');

		await fireEvent.click(screen.getByText('Save Changes'));
		await waitFor(() => expect(screen.getByText('Changes saved')).toBeTruthy());

		// The just-created record (now persisted at id 3) stays selected — the temporary negative id
		// it was selected under no longer matches anything, but the selection must follow the remap
		// rather than falling back to the first record (Alpha).
		const selectedAfterSave = screen.getAllByTestId('workbench-row').find((row) => row.classList.contains('selected'));
		expect(selectedAfterSave?.textContent).toContain('Unnamed');
	});
});
