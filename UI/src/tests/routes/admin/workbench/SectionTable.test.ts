import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

vi.mock('$stores', () => ({ toastError: vi.fn() }));

import SectionTable from '$routes/admin/workbench/components/SectionTable.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified, TableSectionConfig } from '$routes/admin/workbench/entities/types';

interface Spawn {
	zoneId: number;
	weight: number;
}
interface Row extends Identified {
	spawns: Spawn[];
}

const ZONE_OPTS = [
	{ value: 0, text: 'Verdant Hollow' },
	{ value: 1, text: 'Frost Cavern' }
];

const section: TableSectionConfig<Row> = {
	key: 'spawns',
	label: 'Spawns',
	glyph: 'pin',
	kind: 'table',
	itemsKey: 'spawns',
	addLabel: 'Assign zone',
	emptyIcon: 'pin',
	emptyTitle: 'Not assigned to any zone',
	emptySub: 'This enemy will never spawn.',
	newRow: (rec) => ({ zoneId: rec.spawns.some((s) => s.zoneId === 0) ? 1 : 0, weight: 5 }),
	columns: [
		{ key: 'zoneId', label: 'Zone', type: 'select', options: () => ZONE_OPTS, unique: true },
		{ key: 'weight', label: 'Weight', type: 'number', align: 'r' },
		{ key: '__share', label: 'Share', type: 'share', weightKey: 'weight' }
	]
};

const config = (): EntityConfig<Row> =>
	({
		key: 'rows',
		label: 'Rows',
		singular: 'Row',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ id, spawns: [] }),
		meta: () => [],
		sections: [section],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Row>;

const setup = (spawns: Spawn[]) => {
	const store = new EntityStore(config(), [{ id: 1, spawns }]);
	return { store, record: store.items[0], baseline: store.baselineOf(1) };
};

// SectionTable is entity-agnostic (typed against `Identified`); cast the concrete-Row
// section/store to that shape at the single render boundary.
const renderTable = (store: EntityStore<Row>, record: Row | undefined, baseline: Row | undefined) =>
	render(SectionTable, {
		props: {
			section: section as unknown as TableSectionConfig<Identified>,
			record: record as Identified,
			baseline,
			store: store as unknown as EntityStore<Identified>
		}
	});

afterEach(cleanup);

describe('SectionTable', () => {
	it('renders the empty state with an add control when there are no rows', async () => {
		const { store, record, baseline } = setup([]);
		renderTable(store, record, baseline);
		expect(screen.getByText('Not assigned to any zone')).toBeTruthy();
		await fireEvent.click(screen.getByText('Assign zone'));
		expect(store.items[0].spawns).toHaveLength(1);
	});

	it('renders a row per entry with its cells', () => {
		const { store, record, baseline } = setup([{ zoneId: 0, weight: 5 }]);
		const { container } = renderTable(store, record, baseline);
		expect(container.querySelectorAll('tbody tr')).toHaveLength(1);
		expect((container.querySelector('select.sel') as HTMLSelectElement).value).toBe('0');
		// The share column renders a percentage cell.
		expect(container.querySelector('.share-pct')?.textContent).toContain('%');
	});

	it('appends a row via the Add button', async () => {
		const { store, record, baseline } = setup([{ zoneId: 0, weight: 5 }]);
		renderTable(store, record, baseline);
		await fireEvent.click(screen.getByText('Assign zone'));
		expect(store.items[0].spawns).toHaveLength(2);
	});

	it('removes a row via its remove control', async () => {
		const { store, record, baseline } = setup([
			{ zoneId: 0, weight: 5 },
			{ zoneId: 1, weight: 3 }
		]);
		const { container } = renderTable(store, record, baseline);
		await fireEvent.click(container.querySelector('.row-x') as HTMLElement);
		expect(store.items[0].spawns).toEqual([{ zoneId: 1, weight: 3 }]);
	});

	it('edits a cell value through the number input', async () => {
		const { store, record, baseline } = setup([{ zoneId: 0, weight: 5 }]);
		const { container } = renderTable(store, record, baseline);
		const weightInput = container.querySelector('input.num') as HTMLInputElement;
		await fireEvent.input(weightInput, { target: { value: '12' } });
		expect(store.items[0].spawns[0].weight).toBe(12);
	});

	it('disables Add once every unique option is assigned', () => {
		const { store, record, baseline } = setup([
			{ zoneId: 0, weight: 5 },
			{ zoneId: 1, weight: 3 }
		]);
		renderTable(store, record, baseline);
		expect((screen.getByText('Assign zone').closest('button') as HTMLButtonElement).disabled).toBe(true);
	});

	it('marks a newly added row with the added edge', () => {
		const store = new EntityStore(config(), [{ id: 1, spawns: [{ zoneId: 0, weight: 5 }] }]);
		store.patch(1, (d) => d.spawns.push({ zoneId: 1, weight: 2 }));
		const { container } = renderTable(store, store.items[0], store.baselineOf(1));
		const edges = container.querySelectorAll('.row-edge');
		expect((edges[1] as HTMLElement).getAttribute('style')).toContain('var(--change-added)');
	});
});
