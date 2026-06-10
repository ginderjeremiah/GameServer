import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';

vi.mock('$stores', () => ({ toastError: vi.fn() }));

import ChipsSection from '$routes/admin/workbench/components/ChipsSection.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { ChipsSectionConfig, EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

interface Row extends Identified {
	skillPool: number[];
}

const CATALOGUE: { id: number; name: string; baseDamage: number; retired?: boolean }[] = [
	{ id: 1, name: 'Cleave', baseDamage: 12 },
	{ id: 2, name: 'Fireball', baseDamage: 25 },
	{ id: 3, name: 'Heal', baseDamage: 0 },
	{ id: 4, name: 'Smite', baseDamage: 18, retired: true }
];

const section: ChipsSectionConfig<Row> = {
	key: 'skills',
	label: 'Skills',
	glyph: 'rune',
	kind: 'chips',
	itemsKey: 'skillPool',
	catalogue: () => CATALOGUE,
	labelOf: (e) => e.name,
	metaOf: (e) => `${(e as unknown as { baseDamage: number }).baseDamage} dmg`,
	emptyIcon: 'rune',
	emptyTitle: 'No skills in pool',
	emptySub: "Enemies with no skills can't act.",
	addLabel: 'Add skill…'
};

const config = (): EntityConfig<Row> =>
	({
		key: 'rows',
		label: 'Rows',
		singular: 'Row',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ id, skillPool: [] }),
		meta: () => [],
		sections: [section],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Row>;

const setup = (skillPool: number[]) => {
	const store = new EntityStore(config(), [{ id: 1, skillPool }]);
	return { store, record: store.items[0], baseline: store.baselineOf(1) };
};

// The workbench section components are entity-agnostic (typed against `Identified`);
// cast the concrete-Row store/section to that shape at the single render boundary.
const renderChips = (store: EntityStore<Row>, record: Row | undefined, baseline: Row | undefined) =>
	render(ChipsSection, {
		props: {
			section: section as unknown as ChipsSectionConfig<Identified>,
			record: record as Identified,
			baseline,
			store: store as unknown as EntityStore<Identified>
		}
	});

afterEach(cleanup);

describe('ChipsSection', () => {
	it('shows the empty state when no chips are assigned', () => {
		const { store, record, baseline } = setup([]);
		renderChips(store, record, baseline);
		expect(screen.getByText('No skills in pool')).toBeTruthy();
	});

	it('renders a chip with the catalogue label and meta for each assigned id', () => {
		const { store, record, baseline } = setup([1, 2]);
		const { container } = renderChips(store, record, baseline);
		expect(container.querySelectorAll('.skill-chip')).toHaveLength(2);
		expect(screen.getByText('Cleave')).toBeTruthy();
		expect(screen.getByText('25 dmg')).toBeTruthy();
	});

	it('offers only the not-yet-assigned catalogue entries in the add select', () => {
		const { store, record, baseline } = setup([1]);
		const { container } = renderChips(store, record, baseline);
		const addSelect = container.querySelector('.add-select select') as HTMLSelectElement;
		// One placeholder option + the two remaining (Fireball, Heal).
		expect(addSelect.querySelectorAll('option')).toHaveLength(3);
	});

	it('adds a chip when an option is chosen', async () => {
		const { store, record, baseline } = setup([1]);
		const { container } = renderChips(store, record, baseline);
		const addSelect = container.querySelector('.add-select select') as HTMLSelectElement;
		await fireEvent.change(addSelect, { target: { value: '2' } });
		expect(store.items[0].skillPool).toEqual([1, 2]);
	});

	it('removes a chip when its remove control is activated', async () => {
		const { store, record, baseline } = setup([1, 2]);
		const { container } = renderChips(store, record, baseline);
		const removeFirst = container.querySelector('.skill-chip .x') as HTMLElement;
		await fireEvent.click(removeFirst);
		expect(store.items[0].skillPool).toEqual([2]);
	});

	it('excludes a retired catalogue entry from the add select but keeps an assigned one as a chip', () => {
		const { store, record, baseline } = setup([4]);
		const { container } = renderChips(store, record, baseline);
		// The retired skill is still rendered as a chip (name resolves) and marked retired.
		const chip = container.querySelector('.skill-chip') as HTMLElement;
		expect(chip.classList.contains('retired')).toBe(true);
		expect(screen.getByText('Smite')).toBeTruthy();
		expect(screen.getByText('retired')).toBeTruthy();
		// …but it is not offered for fresh assignment (only Cleave, Fireball, Heal remain).
		const addSelect = container.querySelector('.add-select select') as HTMLSelectElement;
		expect(addSelect.querySelectorAll('option')).toHaveLength(4);
		expect(screen.queryByText('Smite · 18 dmg')).toBeNull();
	});

	it('flags a chip added since the baseline', () => {
		const store = new EntityStore(config(), [{ id: 1, skillPool: [1] }]);
		store.patch(1, (d) => d.skillPool.push(2));
		const { container } = renderChips(store, store.items[0], store.baselineOf(1));
		// The second chip (id 2) is not in the baseline → flagged "added".
		const chips = container.querySelectorAll('.skill-chip');
		expect(chips[1].classList.contains('added')).toBe(true);
		expect(chips[0].classList.contains('added')).toBe(false);
	});
});
