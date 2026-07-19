import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';

import ListRow from '$routes/admin/workbench/components/ListRow.svelte';

const meta = createRawSnippet(() => ({ render: () => '<span>meta</span>' }));

const baseProps = {
	testId: 'row',
	selected: false,
	status: 'clean' as const,
	name: 'Fire',
	blank: 'Unnamed',
	onSelect: vi.fn(),
	meta
};

afterEach(cleanup);

describe('ListRow', () => {
	it('shows the "new" spill and added edge accent for an added record', () => {
		const { container } = render(ListRow, { props: { ...baseProps, status: 'added' } });
		expect(screen.getByText('new')).toBeTruthy();
		expect(container.querySelector('.row-edge')?.classList.contains('added')).toBe(true);
	});

	it('shows the "edited" spill and modified edge accent for a modified record', () => {
		const { container } = render(ListRow, { props: { ...baseProps, status: 'modified' } });
		expect(screen.getByText('edited')).toBeTruthy();
		expect(container.querySelector('.row-edge')?.classList.contains('modified')).toBe(true);
	});

	it('dims and strikes a deleted record, with no spill or warnings shown', () => {
		const { container } = render(ListRow, {
			props: { ...baseProps, status: 'deleted', warnings: ['Missing name'] }
		});
		expect(container.querySelector('.list-row')?.classList.contains('deleted')).toBe(true);
		expect(screen.queryByText('new')).toBeNull();
		expect(screen.queryByText('edited')).toBeNull();
		// A deleted record's warnings are moot — the row is going away — so the triangle stays hidden.
		expect(container.querySelector('.warn-tri')).toBeNull();
	});

	it('renders a WarnTriangle with the joined warnings as its tooltip title for a non-deleted record', () => {
		const { container } = render(ListRow, {
			props: { ...baseProps, warnings: ['Missing name', 'Missing icon'] }
		});
		const warn = container.querySelector('.warn-tri');
		expect(warn?.getAttribute('title')).toBe('Missing name · Missing icon');
	});

	it('shows the retired spill and dims the row when retired', () => {
		const { container } = render(ListRow, { props: { ...baseProps, retired: true } });
		expect(screen.getByText('retired')).toBeTruthy();
		expect(container.querySelector('.list-row')?.classList.contains('retired')).toBe(true);
	});

	it('falls back to the blank placeholder when name is empty', () => {
		const { container } = render(ListRow, { props: { ...baseProps, name: '' } });
		const row = container.querySelector('.row-name');
		expect(row?.textContent).toBe('Unnamed');
		expect(row?.classList.contains('blank')).toBe(true);
	});

	it('renders leading content ahead of the row body when provided', () => {
		const leading = createRawSnippet(() => ({ render: () => '<span data-testid="ordinal">0</span>' }));
		const { getByTestId } = render(ListRow, { props: { ...baseProps, leading } });
		expect(getByTestId('ordinal')).toBeTruthy();
	});

	it('renders the provided meta snippet', () => {
		const { container } = render(ListRow, { props: baseProps });
		expect(container.querySelector('.meta')?.textContent).toBe('meta');
	});
});
