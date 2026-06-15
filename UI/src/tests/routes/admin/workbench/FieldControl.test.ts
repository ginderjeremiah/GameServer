import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';

// EntityStore imports toastError from $stores.
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import FieldControl from '$routes/admin/workbench/components/FieldControl.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, FieldConfig, Identified } from '$routes/admin/workbench/entities/types';

interface Row extends Identified {
	name: string;
	count: number;
	enabled: boolean;
	notes: string;
	choice: number;
}

const config = (): EntityConfig<Row> =>
	({
		key: 'rows',
		label: 'Rows',
		singular: 'Row',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ id, name: '', count: 0, enabled: false, notes: '', choice: 1 }),
		meta: () => [],
		sections: [],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Row>;

const seed = (): Row[] => [{ id: 1, name: 'Alpha', count: 5, enabled: false, notes: 'hi', choice: 1 }];

const field = (over: Partial<FieldConfig<Row>> & Pick<FieldConfig<Row>, 'key' | 'label' | 'type'>): FieldConfig<Row> =>
	over as FieldConfig<Row>;

const setup = () => {
	const store = new EntityStore(config(), seed());
	return { store, record: store.items[0], baseline: store.baselineOf(1) };
};

// FieldControl is entity-agnostic (typed against `Identified`); cast the concrete-Row
// field/store to that shape at the single render boundary.
const renderField = (f: FieldConfig<Row>, store: EntityStore<Row>, record: Row, baseline: Row | undefined) =>
	render(FieldControl, {
		props: {
			field: f as unknown as FieldConfig<Identified>,
			record: record as Identified,
			baseline,
			store: store as unknown as EntityStore<Identified>
		}
	});

afterEach(cleanup);

describe('FieldControl — text field', () => {
	it('renders the value and patches the store on input', async () => {
		const { store, record, baseline } = setup();
		const { container } = renderField(field({ key: 'name', label: 'Name', type: 'text' }), store, record, baseline);
		const input = container.querySelector('input.inp') as HTMLInputElement;
		expect(input.value).toBe('Alpha');
		await fireEvent.input(input, { target: { value: 'Beta' } });
		expect(store.items[0].name).toBe('Beta');
	});

	it('shows a required warning when the value is empty', () => {
		const store = new EntityStore(config(), [{ id: 1, name: '', count: 0, enabled: false, notes: '', choice: 1 }]);
		const { container } = renderField(
			field({ key: 'name', label: 'Name', type: 'text', required: true, reqMsg: 'Missing name' }),
			store,
			store.items[0],
			store.baselineOf(1)
		);
		expect((container.querySelector('.lbl') as HTMLElement).classList.contains('warn')).toBe(true);
	});
});

describe('FieldControl — number field', () => {
	it('renders a numeric input and marks dirty against a differing baseline', () => {
		const { store, record } = setup();
		const baseline = { ...record, count: 99 }; // differs → dirty
		const { container } = renderField(
			field({ key: 'count', label: 'Count', type: 'number', suffix: 'pts' }),
			store,
			record,
			baseline
		);
		expect(container.querySelector('.num-unit')).toBeTruthy();
		expect(container.querySelector('.suffix')?.textContent).toBe('pts');
		expect(container.querySelector('.dirty-dot')).toBeTruthy();
	});
});

describe('FieldControl — toggle field', () => {
	it('renders a switch and flips the value on click', async () => {
		const { store, record, baseline } = setup();
		const { container } = renderField(
			field({ key: 'enabled', label: 'On', type: 'toggle', onLabel: 'Yes', offLabel: 'No' }),
			store,
			record,
			baseline
		);
		const toggle = container.querySelector('.toggle') as HTMLElement;
		expect(toggle.getAttribute('aria-checked')).toBe('false');
		await fireEvent.click(toggle);
		expect(store.items[0].enabled).toBe(true);
	});

	it('renders the switch as a native, labelled button (keyboard-operable for free)', () => {
		const { store, record, baseline } = setup();
		const { container } = renderField(
			field({ key: 'enabled', label: 'On', type: 'toggle', onLabel: 'Yes', offLabel: 'No' }),
			store,
			record,
			baseline
		);
		// A real <button role="switch"> gets focus + Enter/Space activation natively, so there is no
		// hand-rolled keydown handler to test — assert the accessible element instead.
		const toggle = container.querySelector('.toggle') as HTMLElement;
		expect(toggle.tagName).toBe('BUTTON');
		expect(toggle.getAttribute('role')).toBe('switch');
		expect(toggle.getAttribute('aria-label')).toBe('On');
	});
});

describe('FieldControl — textarea field', () => {
	it('renders a textarea bound to the value and patches on input', async () => {
		const { store, record, baseline } = setup();
		const { container } = renderField(
			field({ key: 'notes', label: 'Notes', type: 'textarea' }),
			store,
			record,
			baseline
		);
		const area = container.querySelector('textarea.txtarea') as HTMLTextAreaElement;
		expect(area.value).toBe('hi');
		await fireEvent.input(area, { target: { value: 'changed' } });
		expect(store.items[0].notes).toBe('changed');
	});
});

describe('FieldControl — select field', () => {
	it('renders the provided options and patches the numeric value on change', async () => {
		const { store, record, baseline } = setup();
		const { container } = renderField(
			field({
				key: 'choice',
				label: 'Choice',
				type: 'select',
				options: () => [
					{ value: 1, text: 'One' },
					{ value: 2, text: 'Two' }
				]
			}),
			store,
			record,
			baseline
		);
		const select = container.querySelector('select.sel') as HTMLSelectElement;
		expect(select.querySelectorAll('option')).toHaveLength(2);
		await fireEvent.change(select, { target: { value: '2' } });
		expect(store.items[0].choice).toBe(2);
	});
});
