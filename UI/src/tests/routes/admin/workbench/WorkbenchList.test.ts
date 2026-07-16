import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { flushSync } from 'svelte';

// entity-store.svelte.ts imports toastError from $stores; reference.svelte.ts
// (imported transitively via SectionRenderer) imports staticData from $stores.
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
	toastError: vi.fn()
}));

import WorkbenchList from '$routes/admin/workbench/components/WorkbenchList.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

const makeConfig = (retireable = false): EntityConfig<Identified> => ({
	key: 'rows',
	label: 'Enemies',
	singular: 'Enemy',
	glyph: 'box',
	blankName: 'Unnamed',
	retireable,
	newItem: (id) => ({ id, name: '' }),
	meta: () => [],
	sections: [],
	refresh: async () => [],
	persist: async () => ({ records: [], idMap: new Map() })
});

const seed: Identified[] = [
	{ id: 1, name: 'Goblin' },
	{ id: 2, name: 'Orc' },
	{ id: 3, name: 'Dragon' }
];

afterEach(cleanup);

describe('WorkbenchList', () => {
	it('renders a row for each item in the store', () => {
		const store = new EntityStore(makeConfig(), seed);
		render(WorkbenchList, {
			props: { entity: makeConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		const rows = screen.getAllByTestId('workbench-row');
		expect(rows.length).toBe(3);
	});

	it('renders the entity label in the list header', () => {
		const store = new EntityStore(makeConfig(), seed);
		render(WorkbenchList, {
			props: { entity: makeConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		expect(screen.getByTestId('workbench-list').textContent).toContain('Enemies');
	});

	it('marks the selected row with the selected class', () => {
		const store = new EntityStore(makeConfig(), seed);
		render(WorkbenchList, {
			props: { entity: makeConfig(), store, selectedId: 2, onSelect: vi.fn(), onNew: vi.fn() }
		});
		const rows = screen.getAllByTestId('workbench-row');
		expect(rows[1].classList.contains('selected')).toBe(true);
	});

	it('calls onNew when the New button is clicked', async () => {
		const onNew = vi.fn();
		const store = new EntityStore(makeConfig(), seed);
		render(WorkbenchList, {
			props: { entity: makeConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew }
		});
		await fireEvent.click(screen.getByTestId('workbench-new'));
		expect(onNew).toHaveBeenCalledTimes(1);
	});

	it('filters rows by the search query', async () => {
		const store = new EntityStore(makeConfig(), seed);
		const { container } = render(WorkbenchList, {
			props: { entity: makeConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'gob' } });
		flushSync();
		expect(screen.getAllByTestId('workbench-row').length).toBe(1);
		expect(screen.getAllByTestId('workbench-row')[0].textContent).toContain('Goblin');
	});

	it('shows the "new" spill badge for added records', () => {
		const store = new EntityStore(makeConfig(), seed);
		store.addItem();
		const { container } = render(WorkbenchList, {
			props: { entity: makeConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		expect(container.textContent).toContain('new');
	});

	it('marks a retired row with the retired class and badge for a retireable entity', () => {
		const store = new EntityStore(makeConfig(true), [
			{ id: 1, name: 'Goblin' },
			{ id: 2, name: 'Orc', retiredAt: '2026-01-01T00:00:00Z' }
		]);
		const { container } = render(WorkbenchList, {
			props: { entity: makeConfig(true), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		const rows = screen.getAllByTestId('workbench-row');
		expect(rows[0].classList.contains('retired')).toBe(false);
		expect(rows[1].classList.contains('retired')).toBe(true);
		expect(container.querySelector('.spill.retired')?.textContent).toBe('retired');
	});

	it('does not show a retired badge for a non-retireable entity even with a retiredAt set', () => {
		// Tags keep hard-delete and never carry the retired affordance.
		const store = new EntityStore(makeConfig(false), [{ id: 1, name: 'Orc', retiredAt: '2026-01-01T00:00:00Z' }]);
		const { container } = render(WorkbenchList, {
			props: { entity: makeConfig(false), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		expect(container.querySelector('.spill.retired')).toBeNull();
	});

	// A nameless entity (e.g. a skill-synthesis recipe) carries no `name`; its row title and the search
	// filter come from the `title(rec)` hook (driving `displayName`) instead.
	const namelessConfig = (): EntityConfig<Identified> => ({
		...makeConfig(),
		// Derive the row title from a non-name field, the way the recipe editor derives it from its result
		// skill (an empty derivation falls back to blankName, mirroring `title: (r) => resultName(r)`).
		title: (rec) => (rec as { result?: string }).result ?? ''
	});
	const namelessSeed: Identified[] = [
		{ id: 1, result: 'Lava' } as Identified,
		{ id: 2, result: 'Steam' } as Identified
	];

	it('renders a nameless entity row from its title hook and falls back to blankName when empty', () => {
		const store = new EntityStore(namelessConfig(), [...namelessSeed, { id: 3 } as Identified]);
		render(WorkbenchList, {
			props: { entity: namelessConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		const rows = screen.getAllByTestId('workbench-row');
		expect(rows[0].textContent).toContain('Lava');
		// The title hook returns '' (no result), so the row shows the blank-name placeholder instead.
		expect(rows[2].querySelector('.blank')?.textContent).toBe('Unnamed');
	});

	it('filters a nameless entity by its derived title, not by name', async () => {
		const store = new EntityStore(namelessConfig(), namelessSeed);
		const { container } = render(WorkbenchList, {
			props: { entity: namelessConfig(), store, selectedId: 1, onSelect: vi.fn(), onNew: vi.fn() }
		});
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'steam' } });
		flushSync();
		const rows = screen.getAllByTestId('workbench-row');
		expect(rows.length).toBe(1);
		expect(rows[0].textContent).toContain('Steam');
	});
});
