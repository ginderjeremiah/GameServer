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
		tagsByCategory: vi.fn<(catId: number) => Tag[]>()
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));

import TagBrowse from '$routes/admin/workbench/components/TagBrowse.svelte';

const TAGS: Tag[] = [
	{ id: 1, name: 'Fire', tagCategoryId: 10 },
	{ id: 2, name: 'Ice', tagCategoryId: 10 },
	{ id: 3, name: 'Holy', tagCategoryId: 20 }
];

beforeEach(() => {
	mockReference.tags = TAGS;
	mockReference.tagCategories = [
		{ id: 10, name: 'Element' },
		{ id: 20, name: 'School' }
	];
	mockReference.tagColor.mockClear();
	mockReference.tagsByCategory.mockImplementation((catId: number) => TAGS.filter((t) => t.tagCategoryId === catId));
});

afterEach(cleanup);

describe('TagBrowse', () => {
	it('lists every category with its total count', () => {
		render(TagBrowse, { props: { ids: [], onToggle: vi.fn() } });
		expect(screen.getByText('Element')).toBeTruthy();
		expect(screen.getByText('School')).toBeTruthy();
	});

	it('shows the first category tags by default', () => {
		render(TagBrowse, { props: { ids: [], onToggle: vi.fn() } });
		expect(screen.getByText('Fire')).toBeTruthy();
		expect(screen.getByText('Ice')).toBeTruthy();
		// Holy belongs to the unselected School category.
		expect(screen.queryByText('Holy')).toBeNull();
	});

	it('switches the shown tags when another category is selected', async () => {
		render(TagBrowse, { props: { ids: [], onToggle: vi.fn() } });
		await fireEvent.click(screen.getByText('School'));
		expect(screen.getByText('Holy')).toBeTruthy();
		expect(screen.queryByText('Fire')).toBeNull();
	});

	it('filters across all categories when searching', async () => {
		const { container } = render(TagBrowse, { props: { ids: [], onToggle: vi.fn() } });
		const input = container.querySelector('input.inp') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'ho' } });
		expect(screen.getByText('Holy')).toBeTruthy();
		expect(screen.queryByText('Fire')).toBeNull();
	});

	it('shows the applied count for a category that has selected tags', () => {
		const { container } = render(TagBrowse, { props: { ids: [1], onToggle: vi.fn() } });
		// Element category has one applied tag (Fire).
		expect(container.querySelector('.cat-count')?.textContent).toBe('1');
	});

	it('marks an applied tag as on', () => {
		const { container } = render(TagBrowse, { props: { ids: [1], onToggle: vi.fn() } });
		const on = container.querySelector('.tag-toggle.on') as HTMLElement;
		expect(on.textContent).toContain('Fire');
	});

	it('calls onToggle with the clicked tag id', async () => {
		const onToggle = vi.fn();
		render(TagBrowse, { props: { ids: [], onToggle } });
		await fireEvent.click(screen.getByText('Ice'));
		expect(onToggle).toHaveBeenCalledWith(2);
	});

	it('shows an empty message when a category has no tags', async () => {
		mockReference.tagsByCategory.mockReturnValue([]);
		render(TagBrowse, { props: { ids: [], onToggle: vi.fn() } });
		expect(screen.getByText('No tags here.')).toBeTruthy();
	});
});
