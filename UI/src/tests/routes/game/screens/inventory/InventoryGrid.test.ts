import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EItemCategory, ERarity } from '$lib/api';
import { BattleAttributes, type Item } from '$lib/battle';

vi.mock('$lib/engine', () => ({
	EEquipmentSlot: { HelmSlot: 0, ChestSlot: 1, LegSlot: 2, BootSlot: 3, WeaponSlot: 4, AccessorySlot: 5 },
	getEquipmentSlotForCategory: vi.fn(),
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
	staticData: { itemMods: [] },
	registerTooltipComponent: vi.fn(() => ({
		setTooltipPosition: vi.fn(),
		showTooltip: vi.fn(),
		hideTooltip: vi.fn()
	}))
}));

import InventoryGrid from '$routes/game/screens/inventory/InventoryGrid.svelte';
import type { InventoryView } from '$routes/game/screens/inventory/inventory-view.svelte';

const makeGridItem = (itemId: number): Item =>
	({
		id: itemId,
		itemId,
		name: `Item ${itemId}`,
		description: '',
		itemCategoryId: EItemCategory.Weapon,
		rarityId: ERarity.Common,
		iconPath: '',
		attributes: [],
		appliedMods: [],
		modSlots: [],
		tags: [],
		equipped: false,
		favorite: false,
		totalAttributes: new BattleAttributes([], false)
	}) as unknown as Item;

const makeView = (items: Item[] = [], overrides: Partial<InventoryView> = {}): InventoryView =>
	({
		visible: items,
		selectedId: null,
		dragItemId: null,
		page: 0,
		filterCat: null,
		favOnly: false,
		sort: 'category',
		counts: { all: items.length, cats: {} as Record<EItemCategory, number>, fav: 0 },
		select: vi.fn(),
		toggleEquip: vi.fn(),
		toggleFavorite: vi.fn(),
		...overrides
	}) as unknown as InventoryView;

afterEach(cleanup);

describe('InventoryGrid', () => {
	it('shows the empty message when there are no visible items', () => {
		const { getByText } = render(InventoryGrid, { props: { view: makeView([]) } });
		expect(getByText('No items match this filter.')).toBeTruthy();
	});

	it('shows the item count in the footer', () => {
		const items = [makeGridItem(1), makeGridItem(2), makeGridItem(3)];
		const { getByText } = render(InventoryGrid, { props: { view: makeView(items) } });
		expect(getByText('3 items')).toBeTruthy();
	});

	it('does not show pager when items fit on one page', () => {
		const items = Array.from({ length: 10 }, (_, i) => makeGridItem(i + 1));
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		expect(container.querySelector('.pager')).toBeNull();
	});

	it('shows the pager when items exceed a single page', () => {
		const items = Array.from({ length: 50 }, (_, i) => makeGridItem(i + 1));
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		expect(container.querySelector('.pager')).toBeTruthy();
	});

	it('disables the previous-page button on the first page', () => {
		const items = Array.from({ length: 50 }, (_, i) => makeGridItem(i + 1));
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		const prevBtn = container.querySelector('.page-btn') as HTMLButtonElement;
		expect(prevBtn.disabled).toBe(true);
	});

	it('renders one GridSlot per visible item on the current page', () => {
		const items = [makeGridItem(1), makeGridItem(2)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		const slots = container.querySelectorAll('.grid-slot');
		expect(slots.length).toBe(2);
	});
});
