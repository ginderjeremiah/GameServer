import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

interface Tag {
	id: number;
	name: string;
	tagCategoryId: number;
}

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		tags: [] as Tag[],
		tagCategories: [] as { id: number; name: string }[],
		tagColor: vi.fn(() => ({ fg: '#aaa', bd: '#888', bg: '#222' })),
		tagsByCategory: vi.fn<(catId: number) => Tag[]>(),
		tagById: vi.fn<(id: number) => Tag | undefined>()
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import TagsSection from '$routes/admin/workbench/components/TagsSection.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified, TagsSectionConfig } from '$routes/admin/workbench/entities/types';

interface Row extends Identified {
	tags: number[];
}

const TAGS: Tag[] = [
	{ id: 1, name: 'Fire', tagCategoryId: 10 },
	{ id: 2, name: 'Ice', tagCategoryId: 10 }
];

const section: TagsSectionConfig<Row> = {
	key: 'tags',
	label: 'Tags',
	glyph: 'tag',
	kind: 'tags',
	itemsKey: 'tags'
};

const config = (): EntityConfig<Row> =>
	({
		key: 'rows',
		label: 'Rows',
		singular: 'Row',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ id, tags: [] }),
		meta: () => [],
		sections: [section],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Row>;

const setup = (tags: number[]) => {
	const store = new EntityStore(config(), [{ id: 1, tags }]);
	return { store, record: store.items[0], baseline: store.baselineOf(1) };
};

// TagsSection is entity-agnostic (typed against `Identified`); cast the concrete-Row
// section/store to that shape at the single render boundary.
const renderTags = (store: EntityStore<Row>, record: Row | undefined, baseline: Row | undefined) =>
	render(TagsSection, {
		props: {
			section: section as unknown as TagsSectionConfig<Identified>,
			record: record as Identified,
			baseline,
			store: store as unknown as EntityStore<Identified>
		}
	});

beforeEach(() => {
	mockReference.tags = TAGS;
	mockReference.tagCategories = [{ id: 10, name: 'Element' }];
	mockReference.tagColor.mockClear();
	mockReference.tagsByCategory.mockImplementation((catId: number) => TAGS.filter((t) => t.tagCategoryId === catId));
	mockReference.tagById.mockImplementation((id: number) => TAGS.find((t) => t.id === id));
});

afterEach(cleanup);

describe('TagsSection', () => {
	it('shows the applied count and a pill per applied tag', () => {
		const { store, record, baseline } = setup([1]);
		const { container } = renderTags(store, record, baseline);
		expect(screen.getByText('Applied · 1')).toBeTruthy();
		expect(container.querySelectorAll('.tag-pill')).toHaveLength(1);
	});

	it('shows the empty hint when no tags are applied', () => {
		const { store, record, baseline } = setup([]);
		renderTags(store, record, baseline);
		expect(screen.getByText(/No tags yet/)).toBeTruthy();
	});

	it('defaults to browse mode and switches to search mode', async () => {
		const { store, record, baseline } = setup([]);
		const { container } = renderTags(store, record, baseline);
		// Browse mode renders the category rail.
		expect(container.querySelector('.tag-browse')).toBeTruthy();
		await fireEvent.click(screen.getByText('Search'));
		// Search mode swaps in the TagSearch input summary; the browse rail is gone.
		expect(container.querySelector('.tag-browse')).toBeNull();
	});

	it('adds a tag when toggled on in browse mode', async () => {
		const { store, record, baseline } = setup([]);
		renderTags(store, record, baseline);
		await fireEvent.click(screen.getByText('Fire'));
		expect(store.items[0].tags).toEqual([1]);
	});

	it('removes an applied tag via its pill remove control', async () => {
		const { store, record, baseline } = setup([1]);
		const { container } = renderTags(store, record, baseline);
		await fireEvent.click(container.querySelector('.tag-pill .x') as HTMLElement);
		expect(store.items[0].tags).toEqual([]);
	});
});
