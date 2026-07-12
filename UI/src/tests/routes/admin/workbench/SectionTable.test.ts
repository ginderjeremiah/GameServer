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
	rowKey: 'zoneId',
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

	it('disables the empty-state Add control (instead of crashing) when the option catalogue is empty', () => {
		const emptyOptsSection: TableSectionConfig<Row> = {
			...section,
			columns: [
				{ key: 'zoneId', label: 'Zone', type: 'select', options: () => [], unique: true },
				{ key: 'weight', label: 'Weight', type: 'number', align: 'r' },
				{ key: '__share', label: 'Share', type: 'share', weightKey: 'weight' }
			]
		};
		const { store, record, baseline } = setup([]);
		render(SectionTable, {
			props: {
				section: emptyOptsSection as unknown as TableSectionConfig<Identified>,
				record: record as Identified,
				baseline,
				store: store as unknown as EntityStore<Identified>
			}
		});
		const button = screen.getByText('Assign zone').closest('button') as HTMLButtonElement;
		expect(button.disabled).toBe(true);
		expect(button.title).toBe('Every option is already assigned');
	});

	it('marks a newly added row with the added edge', () => {
		const store = new EntityStore(config(), [{ id: 1, spawns: [{ zoneId: 0, weight: 5 }] }]);
		store.patch(1, (d) => d.spawns.push({ zoneId: 1, weight: 2 }));
		const { container } = renderTable(store, store.items[0], store.baselineOf(1));
		const edges = container.querySelectorAll('.row-edge');
		expect((edges[1] as HTMLElement).getAttribute('style')).toContain('var(--change-added)');
	});

	it('keeps a clean row clean after a non-last row is removed (identity, not index, diffing)', async () => {
		// Removing the first of two baseline-equal rows must not make the surviving row flash
		// "modified" — index-keyed diffing compared it against the wrong (shifted) baseline.
		const { store, record, baseline } = setup([
			{ zoneId: 0, weight: 5 },
			{ zoneId: 1, weight: 3 }
		]);
		const { container } = renderTable(store, record, baseline);
		await fireEvent.click(container.querySelector('.row-x') as HTMLElement);

		expect(store.items[0].spawns).toEqual([{ zoneId: 1, weight: 3 }]);
		// A clean row gets the transparent edge (no glow); index-keyed diffing would have shown
		// the survivor as modified by comparing it against the wrong (shifted) baseline.
		const edge = container.querySelector('.row-edge') as HTMLElement;
		expect(edge.getAttribute('style')).toContain('box-shadow: none');
		expect(edge.getAttribute('style')).not.toContain('var(--change-modified)');
	});
});

interface Effect {
	id: number;
	amount: number;
}
interface EffectRow extends Identified {
	effects: Effect[];
}

const effectSection: TableSectionConfig<EffectRow> = {
	key: 'effects',
	label: 'Effects',
	glyph: 'bolt',
	kind: 'table',
	itemsKey: 'effects',
	rowKey: 'id',
	addLabel: 'Add effect',
	emptyIcon: 'bolt',
	emptyTitle: 'No effects',
	emptySub: '',
	newRow: () => ({ id: 0, amount: 0 }),
	columns: [{ key: 'amount', label: 'Amount', type: 'number' }]
};

describe('SectionTable — surrogate-id collections', () => {
	const config = (): EntityConfig<EffectRow> =>
		({
			key: 'rows',
			label: 'Rows',
			singular: 'Row',
			glyph: 'box',
			blankName: 'Unnamed',
			newItem: (id: number) => ({ id, effects: [] }),
			meta: () => [],
			sections: [effectSection],
			refresh: async () => [],
			persist: async () => []
		}) as unknown as EntityConfig<EffectRow>;

	it('assigns each new row a unique negative id so identities never collide', async () => {
		const store = new EntityStore(config(), [{ id: 1, effects: [] }]);
		render(SectionTable, {
			props: {
				section: effectSection as unknown as TableSectionConfig<Identified>,
				record: store.items[0] as Identified,
				baseline: store.baselineOf(1),
				store: store as unknown as EntityStore<Identified>
			}
		});

		await fireEvent.click(screen.getByText('Add effect'));
		await fireEvent.click(screen.getByText('Add effect'));

		const ids = store.items[0].effects.map((e) => e.id);
		expect(ids.every((id) => id < 0)).toBe(true);
		expect(new Set(ids).size).toBe(ids.length);
	});
});

interface Step {
	ordinal: number;
	text: string;
	anchorKey?: string;
}
interface StepRow extends Identified {
	steps: Step[];
}

// Mirrors the lesson entity's "Tour Steps" section: `ordinal` is both the row identity (rowKey) and
// an author-editable number column, unlike every other table's rowKey (a surrogate id or a `unique`
// select/attribute the UI itself keeps collision-free). `anchorKey` mirrors the optional Anchor Key
// column (blank stores as unset rather than "").
const stepSection: TableSectionConfig<StepRow> = {
	key: 'steps',
	label: 'Steps',
	glyph: 'map',
	kind: 'table',
	itemsKey: 'steps',
	rowKey: 'ordinal',
	addLabel: 'Add step',
	emptyIcon: 'map',
	emptyTitle: 'No steps',
	emptySub: '',
	newRow: (rec) => ({ ordinal: rec.steps.length, text: '', anchorKey: undefined }),
	columns: [
		{ key: 'ordinal', label: 'Step', type: 'number', align: 'r' },
		{ key: 'text', label: 'Text', type: 'text' },
		{ key: 'anchorKey', label: 'Anchor Key', type: 'text', optional: true }
	]
};

describe('SectionTable — editable-identity (rowKey) collections', () => {
	const config = (): EntityConfig<StepRow> =>
		({
			key: 'rows',
			label: 'Rows',
			singular: 'Row',
			glyph: 'box',
			blankName: 'Unnamed',
			newItem: (id: number) => ({ id, steps: [] }),
			meta: () => [],
			sections: [stepSection],
			refresh: async () => [],
			persist: async () => []
		}) as unknown as EntityConfig<StepRow>;

	it('swaps identities instead of duplicating them when an edit collides with a sibling row', async () => {
		const store = new EntityStore(config(), [
			{
				id: 1,
				steps: [
					{ ordinal: 0, text: 'First' },
					{ ordinal: 1, text: 'Second' }
				]
			}
		]);
		const { container } = render(SectionTable, {
			props: {
				section: stepSection as unknown as TableSectionConfig<Identified>,
				record: store.items[0] as Identified,
				baseline: store.baselineOf(1),
				store: store as unknown as EntityStore<Identified>
			}
		});

		// Editing the first row's ordinal (0) to the second row's ordinal (1) must not leave two
		// rows sharing ordinal 1 — that duplicate key crashes Svelte's keyed {#each}.
		const ordinalInputs = container.querySelectorAll('input.num');
		await fireEvent.input(ordinalInputs[0], { target: { value: '1' } });

		const ordinals = store.items[0].steps.map((s) => s.ordinal).sort();
		expect(ordinals).toEqual([0, 1]);
		expect(new Set(ordinals).size).toBe(2);
		// The edited row took the new ordinal; its former sibling was swapped to the vacated one.
		expect(store.items[0].steps.find((s) => s.text === 'First')?.ordinal).toBe(1);
		expect(store.items[0].steps.find((s) => s.text === 'Second')?.ordinal).toBe(0);
	});

	it('typing into then erasing an optional cell returns the row to baseline-equal (not dirty)', async () => {
		// Baseline (and the initial draft) omit anchorKey entirely — the "centered callout" state. A
		// typed-then-erased edit must land back on that same unset state, not a stray "".
		const store = new EntityStore(config(), [{ id: 1, steps: [{ ordinal: 0, text: 'Welcome' }] }]);
		const { container } = render(SectionTable, {
			props: {
				section: stepSection as unknown as TableSectionConfig<Identified>,
				record: store.items[0] as Identified,
				baseline: store.baselineOf(1),
				store: store as unknown as EntityStore<Identified>
			}
		});

		const anchorInput = screen.getByLabelText('Anchor Key') as HTMLInputElement;
		await fireEvent.input(anchorInput, { target: { value: 'nav-fight' } });
		await fireEvent.input(anchorInput, { target: { value: '' } });

		expect(store.items[0].steps[0].anchorKey).toBeUndefined();
		const edge = container.querySelector('.row-edge') as HTMLElement;
		expect(edge.getAttribute('style')).toContain('box-shadow: none');
		expect(edge.getAttribute('style')).not.toContain('var(--change-modified)');
	});
});
