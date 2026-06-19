import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EItemCategory, ERarity } from '$lib/api';
import { BattleAttributes, type Item } from '$lib/battle';

// The grid consumes the screen-level item-tooltip controller via context; here we stub that context
// hook to assert the grid drives the shared controller (and honours its suppression rule).
const controller = { describedById: 'tooltip-1', show: vi.fn(), move: vi.fn(), hide: vi.fn() };
vi.mock('$routes/game/screens/inventory/item-tooltip.svelte', () => ({
	getItemTooltip: () => controller
}));

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
	staticData: { itemMods: [] }
}));

import InventoryGrid from '$routes/game/screens/inventory/InventoryGrid.svelte';
import { InventoryView } from '$routes/game/screens/inventory/inventory-view.svelte';

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

afterEach(() => {
	cleanup();
	vi.clearAllMocks();
});

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

	it('disables the next-page button when on the last page', () => {
		const items = Array.from({ length: 50 }, (_, i) => makeGridItem(i + 1));
		// page: 1 puts us on the last page (pages = ceil(50/48) = 2, pageClamped = min(1, 1) = 1).
		const { container } = render(InventoryGrid, { props: { view: makeView(items, { page: 1 }) } });
		const pageBtns = container.querySelectorAll('.page-btn') as NodeListOf<HTMLButtonElement>;
		const nextBtn = pageBtns[1];
		expect(nextBtn.disabled).toBe(true);
	});

	it('enables the previous-page button when not on the first page', () => {
		const items = Array.from({ length: 50 }, (_, i) => makeGridItem(i + 1));
		const { container } = render(InventoryGrid, { props: { view: makeView(items, { page: 1 }) } });
		const pageBtns = container.querySelectorAll('.page-btn') as NodeListOf<HTMLButtonElement>;
		const prevBtn = pageBtns[0];
		expect(prevBtn.disabled).toBe(false);
	});

	it('shows the correct page indicator text on the last page', () => {
		const items = Array.from({ length: 50 }, (_, i) => makeGridItem(i + 1));
		const { container } = render(InventoryGrid, { props: { view: makeView(items, { page: 1 }) } });
		expect(container.querySelector('.page-indicator')!.textContent).toBe('2 / 2');
	});

	it('exposes accessible names on the icon-only pager buttons', () => {
		const items = Array.from({ length: 50 }, (_, i) => makeGridItem(i + 1));
		const { getByRole } = render(InventoryGrid, { props: { view: makeView(items) } });
		// The glyphs (‹/›) carry no accessible name, so screen readers rely on the aria-labels.
		expect(getByRole('button', { name: 'Previous page' })).toBeTruthy();
		expect(getByRole('button', { name: 'Next page' })).toBeTruthy();
	});
});

describe('InventoryGrid — tooltip suppression', () => {
	it('shows the shared tooltip on hover when nothing is selected or dragged', async () => {
		const items = [makeGridItem(1)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		await fireEvent.mouseEnter(container.querySelector('.grid-slot')!);
		expect(controller.show).toHaveBeenCalledWith(items[0], expect.anything());
	});

	it('suppresses the tooltip when an item is selected', async () => {
		const items = [makeGridItem(1)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items, { selectedId: 1 }) } });
		await fireEvent.mouseEnter(container.querySelector('.grid-slot')!);
		expect(controller.show).not.toHaveBeenCalled();
	});

	it('suppresses the tooltip when an item is being dragged', async () => {
		const items = [makeGridItem(1)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items, { dragItemId: 1 }) } });
		await fireEvent.mouseEnter(container.querySelector('.grid-slot')!);
		expect(controller.show).not.toHaveBeenCalled();
	});

	it('surfaces the shared tooltip on keyboard focus of a tile, anchored off its box', async () => {
		const items = [makeGridItem(1)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		const overlay = container.querySelector('.overlay-button')!;
		await fireEvent.focus(overlay);
		expect(controller.show).toHaveBeenCalledWith(items[0], overlay);
	});

	it('applies the suppression rule to keyboard focus too (selected item)', async () => {
		const items = [makeGridItem(1)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items, { selectedId: 1 }) } });
		await fireEvent.focus(container.querySelector('.overlay-button')!);
		expect(controller.show).not.toHaveBeenCalled();
	});

	it('wires the shared tooltip id onto each tile for screen readers', () => {
		const items = [makeGridItem(1)];
		const { container } = render(InventoryGrid, { props: { view: makeView(items) } });
		expect(container.querySelector('.overlay-button')!.getAttribute('aria-describedby')).toBe('tooltip-1');
	});
});

describe('InventoryGrid — drag interactions', () => {
	it('hides the tooltip when a drag starts', async () => {
		const items = [makeGridItem(1)];
		const view = makeView(items);
		const { container } = render(InventoryGrid, { props: { view } });
		await fireEvent.dragStart(container.querySelector('.overlay-button')!, {
			dataTransfer: { setData: vi.fn(), effectAllowed: '' }
		});
		expect(controller.hide).toHaveBeenCalled();
	});

	it('sets view.dragItemId on drag start', async () => {
		const items = [makeGridItem(7)];
		const view = makeView(items);
		const { container } = render(InventoryGrid, { props: { view } });
		await fireEvent.dragStart(container.querySelector('.overlay-button')!, {
			dataTransfer: { setData: vi.fn(), effectAllowed: '' }
		});
		expect(view.dragItemId).toBe(7);
	});

	it('clears view.dragItemId on drag end', async () => {
		const items = [makeGridItem(7)];
		const view = makeView(items, { dragItemId: 7 });
		const { container } = render(InventoryGrid, { props: { view } });
		await fireEvent.dragEnd(container.querySelector('.overlay-button')!);
		expect(view.dragItemId).toBeNull();
	});
});
