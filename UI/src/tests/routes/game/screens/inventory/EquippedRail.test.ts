import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EItemCategory, ERarity } from '$lib/api';
import type { Item } from '$lib/battle';

const { setTooltipPosition, showTooltip, hideTooltip } = vi.hoisted(() => ({
	setTooltipPosition: vi.fn(),
	showTooltip: vi.fn(),
	hideTooltip: vi.fn()
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
	staticData: { itemMods: [] },
	registerTooltipComponent: vi.fn(() => ({ setTooltipPosition, showTooltip, hideTooltip }))
}));

import EquippedRail from '$routes/game/screens/inventory/EquippedRail.svelte';
import type { InventoryView } from '$routes/game/screens/inventory/inventory-view.svelte';

afterEach(cleanup);

const makeItem = (overrides: Partial<Item> = {}): Item =>
	({
		id: 1,
		itemId: 1,
		name: 'Iron Sword',
		description: '',
		itemCategoryId: EItemCategory.Weapon,
		rarityId: ERarity.Common,
		iconPath: '',
		attributes: [],
		appliedMods: [],
		modSlots: [],
		tags: [],
		equipped: true,
		favorite: false,
		equipmentSlotId: 4,
		...overrides
	}) as unknown as Item;

const makeView = (overrides: Partial<InventoryView> = {}): InventoryView =>
	({
		equippedBySlot: {} as Record<number, Item>,
		dragItem: null as Item | null,
		selected: null as Item | null,
		select: vi.fn(),
		equip: vi.fn(),
		unequip: vi.fn(),
		...overrides
	}) as unknown as InventoryView;

describe('EquippedRail — rendering', () => {
	it('renders the "Equipped" section header', () => {
		const { getByText } = render(EquippedRail, { props: { view: makeView() } });
		expect(getByText('Equipped')).toBeTruthy();
	});

	it('renders both equipment groups: Armor and Arms', () => {
		const { getByText } = render(EquippedRail, { props: { view: makeView() } });
		expect(getByText('Armor')).toBeTruthy();
		expect(getByText('Arms')).toBeTruthy();
	});

	it('renders all six slot labels', () => {
		const { getByText } = render(EquippedRail, { props: { view: makeView() } });
		for (const label of ['Helm', 'Chest', 'Legs', 'Boots', 'Weapon', 'Accessory']) {
			expect(getByText(label)).toBeTruthy();
		}
	});

	it('shows the item name for a filled slot', () => {
		const item = makeItem({ name: 'Blessed Blade' });
		const view = makeView({ equippedBySlot: { 4: item } as Record<number, Item> });
		const { getByText } = render(EquippedRail, { props: { view } });
		expect(getByText('Blessed Blade')).toBeTruthy();
	});

	it('shows "Empty" for an unfilled slot', () => {
		const { getAllByText } = render(EquippedRail, { props: { view: makeView() } });
		// All 6 slots are empty on a default view.
		expect(getAllByText('Empty').length).toBe(6);
	});

	it('shows the disabled "Save loadout" button', () => {
		const { container } = render(EquippedRail, { props: { view: makeView() } });
		const btn = container.querySelector('.loadout-button') as HTMLButtonElement;
		expect(btn.disabled).toBe(true);
	});
});

describe('EquippedRail — drop handling', () => {
	it('calls view.equip when the dragged item category matches the target slot', async () => {
		const dragItem = makeItem({ itemId: 99, itemCategoryId: EItemCategory.Weapon });
		const view = makeView({ dragItem });
		const { container } = render(EquippedRail, { props: { view } });
		// Weapon slot is the 5th equip-tile (index 4): Helm, Chest, Leg, Boot, Weapon, Accessory.
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.drop(tiles[4]);
		expect(view.equip).toHaveBeenCalledWith(99, 4 /* WeaponSlot id */);
	});

	it('does not call view.equip when the dragged category does not match the slot', async () => {
		const dragItem = makeItem({ itemId: 99, itemCategoryId: EItemCategory.Helm });
		const view = makeView({ dragItem });
		const { container } = render(EquippedRail, { props: { view } });
		// Drop a Helm item onto the Weapon slot — category mismatch.
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.drop(tiles[4]);
		expect(view.equip).not.toHaveBeenCalled();
	});

	it('does not call view.equip when there is no dragged item', async () => {
		const view = makeView({ dragItem: null });
		const { container } = render(EquippedRail, { props: { view } });
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.drop(tiles[4]);
		expect(view.equip).not.toHaveBeenCalled();
	});

	it('calls view.unequip with the slot id when the unequip button is clicked', async () => {
		const item = makeItem();
		const view = makeView({ equippedBySlot: { 4: item } as Record<number, Item> });
		const { container } = render(EquippedRail, { props: { view } });
		// Hover over the weapon tile to reveal the unequip button.
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.mouseEnter(tiles[4]);
		await fireEvent.click(container.querySelector('.unequip')!);
		expect(view.unequip).toHaveBeenCalledWith(4);
	});
});

describe('EquippedRail — tooltip handling', () => {
	it('shows the tooltip on hover enter when a slot is filled', async () => {
		showTooltip.mockClear();
		const item = makeItem();
		const view = makeView({ equippedBySlot: { 4: item } as Record<number, Item> });
		const { container } = render(EquippedRail, { props: { view } });
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.mouseEnter(tiles[4]);
		expect(showTooltip).toHaveBeenCalled();
	});

	it('hides the tooltip on hover leave', async () => {
		hideTooltip.mockClear();
		const item = makeItem();
		const view = makeView({ equippedBySlot: { 4: item } as Record<number, Item> });
		const { container } = render(EquippedRail, { props: { view } });
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.mouseEnter(tiles[4]);
		await fireEvent.mouseLeave(tiles[4]);
		expect(hideTooltip).toHaveBeenCalled();
	});

	it('updates the tooltip position on hover move', async () => {
		setTooltipPosition.mockClear();
		const item = makeItem();
		const view = makeView({ equippedBySlot: { 4: item } as Record<number, Item> });
		const { container } = render(EquippedRail, { props: { view } });
		const tiles = container.querySelectorAll('.equip-tile');
		await fireEvent.mouseMove(tiles[4]);
		expect(setTooltipPosition).toHaveBeenCalled();
	});
});
