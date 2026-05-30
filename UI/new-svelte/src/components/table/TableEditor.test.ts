import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import TableEditor from './TableEditor.svelte';

// The cell editors don't need the API barrel beyond EChangeType, but TableEditor
// imports it; keep the real enum.
vi.mock('$lib/common', async () => {
	return {
		keys: (obj: object) => Object.keys(obj ?? {}),
		normalizeText: (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase())
	};
});

afterEach(cleanup);

interface Sample {
	id: number;
	name: string;
	power: number;
}

const baseData: Sample[] = [
	{ id: 0, name: 'Cleave', power: 38 },
	{ id: 1, name: 'Guard', power: 0 }
];

const renderEditor = (props: Partial<Record<string, unknown>> = {}) =>
	render(
		TableEditor as never,
		{
			data: baseData.map((d) => ({ ...d })),
			primaryKey: 'id',
			title: 'Add/Edit Skills',
			...props
		} as never
	);

describe('TableEditor', () => {
	it('renders a row per data item with a header', () => {
		renderEditor();
		expect(screen.getByText('Add/Edit Skills')).toBeTruthy();
		expect(document.querySelectorAll('tbody tr').length).toBe(2);
	});

	it('reports no unsaved changes initially', () => {
		renderEditor();
		expect(document.body.textContent).toContain('No unsaved changes');
	});

	it('adds a row when Add Row is clicked', async () => {
		renderEditor();
		await fireEvent.click(screen.getByText('Add Row'));
		expect(document.querySelectorAll('tbody tr').length).toBe(3);
		// New rows show "new" in the id cell and count as a pending change.
		expect(document.body.textContent).toContain('new');
		expect(document.body.textContent).toContain('1 added');
	});

	it('rejects non-numeric input in number fields', async () => {
		renderEditor();
		const numberInput = document.querySelector('input.num') as HTMLInputElement;
		expect(numberInput).toBeTruthy();
		await fireEvent.input(numberInput, { target: { value: '4abc' } });
		expect(numberInput.value).not.toContain('abc');
	});

	it('marks a row modified when a cell changes and clears on Reset', async () => {
		renderEditor();
		const textInput = document.querySelector('input[type="text"]:not(.num)') as HTMLInputElement;
		await fireEvent.input(textInput, { target: { value: 'Cleave Edited' } });
		expect(document.body.textContent).toContain('1 edited');

		const resetButton = screen.getByText('Reset');
		await fireEvent.click(resetButton);
		expect(document.body.textContent).toContain('No unsaved changes');
	});
});
