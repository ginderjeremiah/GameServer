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

const makeConfig = (): EntityConfig<Identified> => ({
	key: 'rows',
	label: 'Enemies',
	singular: 'Enemy',
	glyph: 'box',
	blankName: 'Unnamed',
	newItem: (id) => ({ id, name: '' }),
	meta: () => [],
	sections: [],
	refresh: async () => [],
	persist: async () => []
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
});
