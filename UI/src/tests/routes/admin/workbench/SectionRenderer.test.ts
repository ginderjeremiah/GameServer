import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';

// SectionRenderer dispatches to one child per section kind; several children read
// the workbench reference singleton, so it is stubbed with everything they touch.
const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		tags: [],
		tagCategories: [],
		tagColor: vi.fn(() => ({ fg: '#aaa', bd: '#888', bg: '#222' })),
		tagsByCategory: vi.fn(() => []),
		tagById: vi.fn(() => undefined),
		rarityColor: vi.fn(() => 'var(--rarity-common)'),
		tagUsage: vi.fn(() => ({ items: 0, mods: 0, itemList: [], modList: [] })),
		challengeTypes: [{ id: 1, name: 'Enemies Killed', goalComparison: 1, statisticType: null }],
		entityOptions: vi.fn(() => []),
		entityName: vi.fn(() => null),
		itemRecords: vi.fn(() => []),
		itemModRecords: vi.fn(() => []),
		skillRecords: vi.fn(() => [])
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import SectionRenderer from '$routes/admin/workbench/components/SectionRenderer.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified, SectionConfig } from '$routes/admin/workbench/entities/types';

// A record carrying every field the various section kinds read.
const RECORD = {
	id: 1,
	name: 'Rec',
	tagCategoryId: 5,
	rows: [] as unknown[],
	chipIds: [] as number[],
	tags: [] as number[],
	challengeTypeId: 1,
	entityType: 0,
	statisticType: undefined,
	targetEntityId: undefined,
	progressGoal: 10
};

const config = (): EntityConfig<Identified> =>
	({
		key: 'rows',
		label: 'Rows',
		singular: 'Row',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ ...RECORD, id }),
		meta: () => [],
		sections: [],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Identified>;

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const renderKind = (section: SectionConfig<any>) => {
	const store = new EntityStore(config(), [{ ...RECORD } as unknown as Identified]);
	const record = store.items[0];
	return render(SectionRenderer, { props: { section, record, baseline: store.baselineOf(1), store } });
};

afterEach(cleanup);

describe('SectionRenderer dispatch', () => {
	it('renders a fields section', () => {
		const { container } = renderKind({
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			kind: 'fields',
			fields: [{ key: 'name', label: 'Name', type: 'text' }]
			// eslint-disable-next-line @typescript-eslint/no-explicit-any
		} as any);
		expect(container.querySelector('input.inp')).toBeTruthy();
	});

	it('renders a table section', () => {
		renderKind({
			key: 'rows',
			label: 'Rows',
			glyph: 'bars',
			kind: 'table',
			itemsKey: 'rows',
			addLabel: 'Add row',
			emptyIcon: 'bars',
			emptyTitle: 'TABLE EMPTY MARKER',
			emptySub: '',
			newRow: () => ({ a: 0 }),
			columns: [{ key: 'a', label: 'A', type: 'number' }]
			// eslint-disable-next-line @typescript-eslint/no-explicit-any
		} as any);
		expect(screen.getByText('TABLE EMPTY MARKER')).toBeTruthy();
	});

	it('renders a chips section', () => {
		renderKind({
			key: 'chips',
			label: 'Chips',
			glyph: 'rune',
			kind: 'chips',
			itemsKey: 'chipIds',
			catalogue: () => [],
			labelOf: (e: { name: string }) => e.name,
			metaOf: () => '',
			emptyIcon: 'rune',
			emptyTitle: 'CHIPS EMPTY MARKER',
			emptySub: '',
			addLabel: 'Add'
			// eslint-disable-next-line @typescript-eslint/no-explicit-any
		} as any);
		expect(screen.getByText('CHIPS EMPTY MARKER')).toBeTruthy();
	});

	it('renders a tags section', () => {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		renderKind({ key: 'tags', label: 'Tags', glyph: 'tag', kind: 'tags', itemsKey: 'tags' } as any);
		expect(screen.getByText('Applied · 0')).toBeTruthy();
	});

	it('renders a usage section', () => {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		const { container } = renderKind({ key: 'usage', label: 'Usage', glyph: 'tag', kind: 'usage' } as any);
		expect(container.querySelector('.usage-count')).toBeTruthy();
	});

	it('renders a challenge-condition section', () => {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		renderKind({ key: 'cond', label: 'Condition', glyph: 'target', kind: 'challenge-condition' } as any);
		expect(screen.getByText('Players see')).toBeTruthy();
	});

	it('renders a challenge-reward section', () => {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		renderKind({ key: 'reward', label: 'Reward', glyph: 'gift', kind: 'challenge-reward' } as any);
		expect(screen.getByText('Item Reward')).toBeTruthy();
	});
});
