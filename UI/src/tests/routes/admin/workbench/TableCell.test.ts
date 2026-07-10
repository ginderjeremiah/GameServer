import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import type { ColumnConfig } from '$routes/admin/workbench/entities/types';

import TableCell from '$routes/admin/workbench/components/TableCell.svelte';

afterEach(cleanup);

const makeSelectCol = (overrides: Partial<ColumnConfig> = {}): ColumnConfig => ({
	key: 'zoneId',
	label: 'Zone',
	type: 'select',
	options: () => [
		{ value: 1, text: 'Zone A' },
		{ value: 2, text: 'Zone B' },
		{ value: 3, text: 'Zone C' }
	],
	...overrides
});

const makeNumberCol = (overrides: Partial<ColumnConfig> = {}): ColumnConfig => ({
	key: 'weight',
	label: 'Weight',
	type: 'number',
	...overrides
});

const makeShareCol = (overrides: Partial<ColumnConfig> = {}): ColumnConfig => ({
	key: 'weight',
	label: 'Share',
	type: 'share',
	...overrides
});

const makeTextCol = (overrides: Partial<ColumnConfig> = {}): ColumnConfig => ({
	key: 'text',
	label: 'Callout Text',
	type: 'text',
	...overrides
});

describe('TableCell — select type', () => {
	it('renders a <select> element', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeSelectCol(),
				row: { zoneId: 1 },
				idx: 0,
				rows: [{ zoneId: 1 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('select')).toBeTruthy();
	});

	it('shows all available options', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeSelectCol(),
				row: { zoneId: 1 },
				idx: 0,
				rows: [{ zoneId: 1 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(container.querySelectorAll('option').length).toBe(3);
	});

	it('disables options already chosen in sibling rows when unique=true', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeSelectCol({ unique: true }),
				// This row has zoneId=1; sibling has zoneId=2 → option 2 should be disabled.
				row: { zoneId: 1 },
				idx: 0,
				rows: [{ zoneId: 1 }, { zoneId: 2 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		const options = Array.from(container.querySelectorAll('option')) as HTMLOptionElement[];
		const zone2 = options.find((o) => o.value === '2');
		expect(zone2?.disabled).toBe(true);
	});

	it('does not disable the currently selected option even when it would be taken', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeSelectCol({ unique: true }),
				row: { zoneId: 1 },
				idx: 0,
				// Row 1 selects zoneId=1; the "sibling" filter excludes idx=0, so 1 is not in taken.
				rows: [{ zoneId: 1 }, { zoneId: 2 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		const options = Array.from(container.querySelectorAll('option')) as HTMLOptionElement[];
		const zone1 = options.find((o) => o.value === '1');
		expect(zone1?.disabled).toBe(false);
	});

	it('passes the full row to the options provider, alongside the current value', () => {
		const options = vi.fn(() => [{ value: 1, text: 'Zone A' }]);
		render(TableCell, {
			props: {
				col: makeSelectCol({ options }),
				row: { zoneId: 1, kind: 2 },
				idx: 0,
				rows: [{ zoneId: 1, kind: 2 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(options).toHaveBeenCalledWith(1, { zoneId: 1, kind: 2 });
	});

	it('adds the "dirty" class to the select when dirty=true', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeSelectCol(),
				row: { zoneId: 1 },
				idx: 0,
				rows: [{ zoneId: 1 }],
				record: {},
				dirty: true,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('select')!.classList.contains('dirty')).toBe(true);
	});

	it('calls onChange with the numeric value when the selection changes', async () => {
		const onChange = vi.fn();
		const { container } = render(TableCell, {
			props: {
				col: makeSelectCol(),
				row: { zoneId: 1 },
				idx: 0,
				rows: [{ zoneId: 1 }],
				record: {},
				dirty: false,
				onChange
			}
		});
		const select = container.querySelector('select') as HTMLSelectElement;
		await fireEvent.change(select, { target: { value: '3' } });
		expect(onChange).toHaveBeenCalledWith(3);
	});
});

describe('TableCell — number type', () => {
	it('renders a NumInput (text input with inputmode=decimal)', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeNumberCol(),
				row: { weight: 10 },
				idx: 0,
				rows: [{ weight: 10 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		const input = container.querySelector('input') as HTMLInputElement;
		expect(input).toBeTruthy();
		expect(input.getAttribute('inputmode')).toBe('decimal');
	});
});

describe('TableCell — text type', () => {
	it('renders a text input with the row value', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeTextCol(),
				row: { text: 'Look here' },
				idx: 0,
				rows: [{ text: 'Look here' }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		const input = container.querySelector('input') as HTMLInputElement;
		expect(input).toBeTruthy();
		expect(input.value).toBe('Look here');
	});

	it('falls back to an empty string when the row value is absent', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeTextCol(),
				row: {},
				idx: 0,
				rows: [{}],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect((container.querySelector('input') as HTMLInputElement).value).toBe('');
	});

	it('calls onChange with the typed string', async () => {
		const onChange = vi.fn();
		const { container } = render(TableCell, {
			props: {
				col: makeTextCol(),
				row: { text: '' },
				idx: 0,
				rows: [{ text: '' }],
				record: {},
				dirty: false,
				onChange
			}
		});
		const input = container.querySelector('input') as HTMLInputElement;
		await fireEvent.input(input, { target: { value: 'Tap here to open your inventory' } });
		expect(onChange).toHaveBeenCalledWith('Tap here to open your inventory');
	});

	it('adds the "dirty" class when dirty=true', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeTextCol(),
				row: { text: 'a' },
				idx: 0,
				rows: [{ text: 'a' }],
				record: {},
				dirty: true,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('input')!.classList.contains('dirty')).toBe(true);
	});
});

describe('TableCell — share type', () => {
	it('renders a share bar', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeShareCol(),
				row: { weight: 50 },
				idx: 0,
				rows: [{ weight: 50 }, { weight: 50 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('.share-bar')).toBeTruthy();
	});

	it('shows 50% when the row weight is half of the total', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeShareCol(),
				row: { weight: 50 },
				idx: 0,
				rows: [{ weight: 50 }, { weight: 50 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('.share-pct')!.textContent).toBe('50%');
	});

	it('shows 100% when the row is the only row', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeShareCol(),
				row: { weight: 10 },
				idx: 0,
				rows: [{ weight: 10 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('.share-pct')!.textContent).toBe('100%');
	});

	it('shows 0% when the row weight is 0', () => {
		const { container } = render(TableCell, {
			props: {
				col: makeShareCol(),
				row: { weight: 0 },
				idx: 0,
				rows: [{ weight: 0 }, { weight: 10 }],
				record: {},
				dirty: false,
				onChange: vi.fn()
			}
		});
		expect(container.querySelector('.share-pct')!.textContent).toBe('0%');
	});
});
