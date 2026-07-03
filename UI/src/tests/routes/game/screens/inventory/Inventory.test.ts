import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { tick } from 'svelte';
import { EItemCategory, ERarity } from '$lib/api';
import { BattleAttributes, type Item } from '$lib/battle';

// The mocked manager serves this mutable list so individual tests can stock the grid (empty by default).
const items: Item[] = [];

// InventoryView (constructed inside Inventory.svelte) imports from $lib/engine and $stores.
vi.mock('$lib/engine', () => ({
	EEquipmentSlot: { HelmSlot: 0, ChestSlot: 1, LegSlot: 2, BootSlot: 3, WeaponSlot: 4, AccessorySlot: 5 },
	getEquipmentSlotForCategory: vi.fn((cat: number) => cat - 1),
	inventoryManager: {
		get unlockedItemList() {
			return items;
		},
		get unlockedItems() {
			return new Map(items.map((i) => [i.itemId, i]));
		},
		equippedSlots: [],
		equipmentStats: [],
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
	playerProficiencies: { levelOf: vi.fn(() => 0) },
	anchorPosition: vi.fn(() => ({ x: 0, y: 0 })),
	registerTooltipComponent: vi.fn(() => ({
		setTooltipPosition: vi.fn(),
		showTooltip: vi.fn(),
		hideTooltip: vi.fn()
	}))
}));

import Inventory from '$routes/game/screens/inventory/Inventory.svelte';

const makeItem = (itemId: number): Item =>
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

afterEach(() => {
	cleanup();
	items.length = 0;
});

describe('Inventory screen', () => {
	it('renders the screen container', () => {
		render(Inventory);
		expect(screen.getByTestId('inventory-screen')).toBeTruthy();
	});

	it('renders the "Inventory" title', () => {
		render(Inventory);
		expect(screen.getByText('Inventory')).toBeTruthy();
	});

	it('does not show the detail drawer initially (no item selected)', () => {
		const { container } = render(Inventory);
		// The drawer has class "open" only when an item is selected.
		const drawer = container.querySelector('.drawer') as HTMLElement;
		expect(drawer.classList.contains('open')).toBe(false);
	});

	it('renders the equipped rail', () => {
		const { container } = render(Inventory);
		expect(container.querySelector('.rail')).toBeTruthy();
	});

	it('renders the drawer backdrop as a <button> (not a <div>) for a11y', () => {
		const { container } = render(Inventory);
		const backdrop = container.querySelector('.backdrop');
		expect(backdrop?.tagName.toLowerCase()).toBe('button');
		expect(backdrop?.getAttribute('type')).toBe('button');
		expect(backdrop?.getAttribute('aria-label')).toBe('Close item drawer');
	});
});

describe('Inventory screen — item drawer overlay behavior', () => {
	// Selects the only grid item via its full-bleed primary action, focusing it first so the trap's
	// focus capture/restore can be observed (a jsdom click does not move focus by itself).
	const openDrawer = async (container: HTMLElement) => {
		const tile = container.querySelector('.overlay-button') as HTMLElement;
		tile.focus();
		await fireEvent.click(tile);
		await tick();
		return tile;
	};

	it('opens the drawer as a labelled dialog and captures focus inside it', async () => {
		items.push(makeItem(1));
		const { container } = render(Inventory);
		await openDrawer(container);

		const dialog = container.querySelector('.drawer-content') as HTMLElement;
		expect(dialog.getAttribute('role')).toBe('dialog');
		expect(dialog.getAttribute('aria-modal')).toBe('true');
		expect(dialog.getAttribute('aria-label')).toBe('Item details');
		expect(container.querySelector('.drawer')?.classList.contains('open')).toBe(true);
		// Focus moves onto the drawer's first focusable (the header close button).
		expect(dialog.contains(document.activeElement)).toBe(true);
	});

	it('closes the drawer on Escape', async () => {
		items.push(makeItem(1));
		const { container } = render(Inventory);
		await openDrawer(container);
		expect(container.querySelector('.drawer-content')).not.toBeNull();

		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(container.querySelector('.drawer-content')).toBeNull();
		expect(container.querySelector('.drawer')?.classList.contains('open')).toBe(false);
	});

	it('restores focus to the previously focused element when the drawer closes', async () => {
		items.push(makeItem(1));
		const { container } = render(Inventory);
		const tile = await openDrawer(container);
		expect(document.activeElement).not.toBe(tile);

		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(document.activeElement).toBe(tile);
	});

	it('traps Tab inside the open drawer', async () => {
		items.push(makeItem(1));
		const { container } = render(Inventory);
		await openDrawer(container);

		const dialog = container.querySelector('.drawer-content') as HTMLElement;
		const focusables = dialog.querySelectorAll<HTMLElement>('button:not([disabled])');
		// Tab off the drawer's last focusable wraps back to its first, never out to the grid.
		focusables[focusables.length - 1].focus();
		await fireEvent.keyDown(window, { key: 'Tab' });
		expect(document.activeElement).toBe(focusables[0]);
	});
});
