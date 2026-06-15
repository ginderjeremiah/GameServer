import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EAttribute, EItemCategory, EItemModType, ERarity } from '$lib/api';
import { BattleAttributes, type Item } from '$lib/battle';

// inventory-view.svelte.ts (imported as a type) pulls in $lib/engine at module level.
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

import ItemDrawer from '$routes/game/screens/inventory/ItemDrawer.svelte';
import type { InventoryView } from '$routes/game/screens/inventory/inventory-view.svelte';

const makeItem = (overrides: Partial<Item> = {}): Item =>
	({
		id: 1,
		itemId: 1,
		name: 'Iron Sword',
		description: 'A trusty blade.',
		itemCategoryId: EItemCategory.Weapon,
		rarityId: ERarity.Common,
		iconPath: '',
		attributes: [{ attributeId: EAttribute.Strength, amount: 10 }],
		appliedMods: [],
		modSlots: [],
		tags: [],
		equipped: false,
		equipmentSlotId: undefined,
		favorite: false,
		totalAttributes: new BattleAttributes([{ attributeId: EAttribute.Strength, amount: 10 }], false),
		...overrides
	}) as unknown as Item;

const makeView = (overrides = {}): InventoryView =>
	({
		select: vi.fn(),
		toggleEquip: vi.fn(),
		removeMod: vi.fn(),
		applyMod: vi.fn(),
		compatibleMods: vi.fn(() => []),
		selectedId: null,
		...overrides
	}) as unknown as InventoryView;

afterEach(cleanup);

describe('ItemDrawer', () => {
	it('renders the item name', () => {
		const { getByText } = render(ItemDrawer, { props: { item: makeItem(), view: makeView() } });
		expect(getByText('Iron Sword')).toBeTruthy();
	});

	it('renders the category and rarity labels in the migrated TooltipTitle header', () => {
		const { container, getByText } = render(ItemDrawer, {
			props: { item: makeItem(), view: makeView() }
		});
		// Category label is owned by the shared TooltipTitle primitive.
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Weapon');
		// Rarity tag rides along in TooltipTitle's `trailing` snippet.
		expect(getByText('Common')).toBeTruthy();
	});

	it('shows "Equip" for an unequipped item', () => {
		const { getByText } = render(ItemDrawer, {
			props: { item: makeItem({ equipmentSlotId: undefined }), view: makeView() }
		});
		expect(getByText(/Equip/, { exact: false })).toBeTruthy();
	});

	it('shows "Unequip" for an equipped item', () => {
		const { getByText } = render(ItemDrawer, {
			props: { item: makeItem({ equipmentSlotId: 4 }), view: makeView() }
		});
		expect(getByText(/Unequip/, { exact: false })).toBeTruthy();
	});

	it('calls view.select(null) when the close button is clicked', async () => {
		const view = makeView();
		const { getByLabelText } = render(ItemDrawer, { props: { item: makeItem(), view } });
		await fireEvent.click(getByLabelText('Close'));
		expect(view.select).toHaveBeenCalledWith(null);
	});

	it('renders the mod slots section', () => {
		const item = makeItem({
			modSlots: [{ id: 10, itemId: 1, itemModSlotTypeId: EItemModType.Prefix }]
		});
		const { getByText } = render(ItemDrawer, { props: { item, view: makeView() } });
		expect(getByText(/Mod slots/)).toBeTruthy();
	});

	it('shows "No mod slots" text when item has no mod slots', () => {
		const { getByText } = render(ItemDrawer, { props: { item: makeItem(), view: makeView() } });
		expect(getByText('This item has no mod slots.')).toBeTruthy();
	});

	it('renders the item description when present', () => {
		const { getByText } = render(ItemDrawer, { props: { item: makeItem(), view: makeView() } });
		expect(getByText('A trusty blade.')).toBeTruthy();
	});

	it('renders the Stats section through the shared tooltip stats grid', () => {
		const { container } = render(ItemDrawer, { props: { item: makeItem(), view: makeView() } });
		const grid = container.querySelector('.tt-stats-grid') as HTMLElement;
		expect(grid).toBeTruthy();
		expect(grid.textContent).toContain('Strength');
		expect(grid.textContent).toContain('+10');
	});

	it('derives the Stats from item.totalAttributes, not a manual item+mod flatten', () => {
		// totalAttributes carries an attribute (Agility) absent from `attributes`/`appliedMods`,
		// so it only renders if the drawer reads the item's own merged projection.
		const item = makeItem({
			attributes: [{ attributeId: EAttribute.Strength, amount: 10 }],
			appliedMods: [],
			totalAttributes: new BattleAttributes(
				[
					{ attributeId: EAttribute.Strength, amount: 10 },
					{ attributeId: EAttribute.Agility, amount: 5 }
				],
				false
			)
		});
		const { container } = render(ItemDrawer, { props: { item, view: makeView() } });
		const text = (container.querySelector('.tt-stats-grid') as HTMLElement).textContent ?? '';
		expect(text).toContain('Agility');
		expect(text).toContain('+5');
	});
});
