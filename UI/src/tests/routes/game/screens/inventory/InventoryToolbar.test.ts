import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EItemCategory } from '$lib/api';

// inventory-view.svelte.ts imports from $lib/engine and $stores at module level.
vi.mock('$lib/engine', () => ({
	EEquipmentSlot: { HelmSlot: 0, ChestSlot: 1, LegSlot: 2, BootSlot: 3, WeaponSlot: 4, AccessorySlot: 5 },
	getEquipmentSlotForCategory: vi.fn((cat: number) => cat - 1),
	inventoryManager: {
		get unlockedItemList() {
			return [];
		},
		unlockedMods: new Set<number>(),
		equipItem: vi.fn(),
		unequipItem: vi.fn(),
		setFavorite: vi.fn(),
		applyMod: vi.fn(),
		removeMod: vi.fn()
	}
}));

vi.mock('$stores', () => ({
	staticData: { itemMods: [] }
}));

import InventoryToolbar from '$routes/game/screens/inventory/InventoryToolbar.svelte';
import {
	FILTER_CATEGORIES,
	SORTS,
	type InventoryView,
	type SortKey
} from '$routes/game/screens/inventory/inventory-view.svelte';

const makeView = (overrides: Partial<{ filterCat: EItemCategory | null; favOnly: boolean; sort: SortKey }> = {}) =>
	({
		filterCat: null,
		favOnly: false,
		sort: 'category' as SortKey,
		counts: { all: 10, cats: {} as Record<EItemCategory, number>, fav: 2 },
		...overrides
	}) as unknown as InventoryView;

afterEach(cleanup);

describe('InventoryToolbar', () => {
	it('renders the "All" chip as active when no category filter is set', () => {
		const { container } = render(InventoryToolbar, { props: { view: makeView() } });
		const allChip = Array.from(container.querySelectorAll('button.chip')).find((b) =>
			b.textContent?.includes('All')
		) as HTMLElement;
		expect(allChip).toBeTruthy();
		expect(allChip.classList.contains('active')).toBe(true);
	});

	it('renders a chip for each filter category', () => {
		const { container } = render(InventoryToolbar, { props: { view: makeView() } });
		const chips = Array.from(container.querySelectorAll('button.chip'));
		// One "All" chip, one per FILTER_CATEGORY, one favorites chip
		expect(chips.length).toBe(FILTER_CATEGORIES.length + 2);
	});

	it('renders the Favorites chip', () => {
		const { container } = render(InventoryToolbar, { props: { view: makeView() } });
		const favChip = Array.from(container.querySelectorAll('button.chip.fav'))[0];
		expect(favChip).toBeTruthy();
		expect(favChip.textContent).toContain('Favorites');
	});

	it('renders a sort button for each sort key', () => {
		const { container } = render(InventoryToolbar, { props: { view: makeView() } });
		const sortKeys = Object.keys(SORTS) as SortKey[];
		const sortButtons = container.querySelectorAll('.sort-option');
		expect(sortButtons.length).toBe(sortKeys.length);
	});

	it('marks the active sort button with the active class', () => {
		const { container } = render(InventoryToolbar, { props: { view: makeView({ sort: 'name' }) } });
		const activeSort = Array.from(container.querySelectorAll('.sort-option')).find(
			(b) => b.textContent?.trim() === SORTS.name.label
		) as HTMLElement;
		expect(activeSort).toBeTruthy();
		expect(activeSort.classList.contains('active')).toBe(true);
	});
});
