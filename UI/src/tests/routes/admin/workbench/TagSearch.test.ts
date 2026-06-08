import { describe, it, expect, afterEach, vi, beforeEach } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

// `vi.mock` factories are hoisted to the top of the file by Vitest, so any
// module-level variables they reference must themselves be hoisted via `vi.hoisted`.
const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		tags: [] as { id: number; name: string; tagCategoryId: number }[],
		tagCategories: [] as { id: number; name: string }[],
		tagColor: vi.fn(() => ({ fg: '#aaa', bd: '#888' }))
	}
}));

vi.mock('$routes/admin/workbench/reference.svelte', () => ({
	reference: mockReference
}));

import TagSearch from '$routes/admin/workbench/components/TagSearch.svelte';

afterEach(cleanup);

beforeEach(() => {
	mockReference.tagColor.mockClear();
	mockReference.tags = [
		{ id: 1, name: 'Fire', tagCategoryId: 10 },
		{ id: 2, name: 'Ice', tagCategoryId: 10 },
		{ id: 3, name: 'Thunder', tagCategoryId: 20 }
	];
	mockReference.tagCategories = [
		{ id: 10, name: 'Elements' },
		{ id: 20, name: 'Magic' }
	];
});

describe('TagSearch — initial rendering', () => {
	it('renders the search input', () => {
		const { container } = render(TagSearch, { props: { ids: [], onAdd: vi.fn() } });
		expect(container.querySelector('input')).toBeTruthy();
	});

	it('shows all tags that are not already applied', () => {
		render(TagSearch, { props: { ids: [], onAdd: vi.fn() } });
		expect(screen.getByText('3 matches across 2 categories · click to add')).toBeTruthy();
	});

	it('excludes tags already in the ids list', () => {
		// Tag id=1 already applied — only 2 matches remain.
		render(TagSearch, { props: { ids: [1], onAdd: vi.fn() } });
		expect(screen.getByText('2 matches across 2 categories · click to add')).toBeTruthy();
	});
});

describe('TagSearch — filtering', () => {
	it('filters tags by the search query (case-insensitive)', async () => {
		const { container } = render(TagSearch, { props: { ids: [], onAdd: vi.fn() } });
		const input = container.querySelector('input') as HTMLInputElement;
		// Type "fire" — only the Fire tag matches.
		await fireEvent.input(input, { target: { value: 'fire' } });
		expect(screen.getByText('1 match across 1 category · click to add')).toBeTruthy();
		expect(screen.getByText('Fire')).toBeTruthy();
	});

	it('shows "No matching tags" when the query has no results', async () => {
		const { container } = render(TagSearch, { props: { ids: [], onAdd: vi.fn() } });
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'xyz' } });
		expect(screen.getByText('No matching tags.')).toBeTruthy();
	});

	it('groups results by category', () => {
		render(TagSearch, { props: { ids: [], onAdd: vi.fn() } });
		expect(screen.getByText('Elements')).toBeTruthy();
		expect(screen.getByText('Magic')).toBeTruthy();
	});
});

describe('TagSearch — adding tags', () => {
	it('calls onAdd with the correct tag id when a tag button is clicked', async () => {
		const onAdd = vi.fn();
		render(TagSearch, { props: { ids: [], onAdd } });
		await fireEvent.click(screen.getByText('Fire'));
		expect(onAdd).toHaveBeenCalledWith(1);
	});

	it('calls onAdd with the id of the clicked tag, not adjacent ones', async () => {
		const onAdd = vi.fn();
		render(TagSearch, { props: { ids: [], onAdd } });
		await fireEvent.click(screen.getByText('Thunder'));
		expect(onAdd).toHaveBeenCalledWith(3);
		expect(onAdd).not.toHaveBeenCalledWith(1);
	});
});
