import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		tagColor: vi.fn(() => ({ fg: '#aaa', bd: '#888', bg: '#222' }))
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));

import TagPill from '$routes/admin/workbench/components/TagPill.svelte';

const tag = { id: 1, name: 'Fire', tagCategoryId: 10 };

afterEach(cleanup);

describe('TagPill', () => {
	it('renders the tag name', () => {
		render(TagPill, { props: { tag } });
		expect(screen.getByText('Fire')).toBeTruthy();
	});

	it('omits the remove control when no onRemove is given', () => {
		const { container } = render(TagPill, { props: { tag } });
		expect(container.querySelector('.x')).toBeNull();
	});

	it('calls onRemove when the remove control is clicked', async () => {
		const onRemove = vi.fn();
		const { container } = render(TagPill, { props: { tag, onRemove } });
		await fireEvent.click(container.querySelector('.x') as HTMLElement);
		expect(onRemove).toHaveBeenCalledOnce();
	});

	it('renders the remove control as a real, labelled button', () => {
		const { container } = render(TagPill, { props: { tag, onRemove: vi.fn() } });
		const remove = container.querySelector('.x') as HTMLElement;
		expect(remove.tagName).toBe('BUTTON');
		expect(remove.getAttribute('aria-label')).toBe('Remove tag Fire');
	});

	it('adds the new-pill inset ring only when isNew is set', () => {
		const { container, rerender } = render(TagPill, { props: { tag, isNew: true } });
		expect((container.querySelector('.tag-pill') as HTMLElement).getAttribute('style')).toContain('inset');

		rerender({ tag, isNew: false });
		expect((container.querySelector('.tag-pill') as HTMLElement).getAttribute('style')).toContain('none');
	});
});
