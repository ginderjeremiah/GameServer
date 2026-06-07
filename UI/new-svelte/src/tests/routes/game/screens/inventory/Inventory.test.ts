import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';

// InventoryView (constructed inside Inventory.svelte) imports from $lib/engine and $stores.
vi.mock('$lib/engine', () => ({
	EEquipmentSlot: { HelmSlot: 0, ChestSlot: 1, LegSlot: 2, BootSlot: 3, WeaponSlot: 4, AccessorySlot: 5 },
	getEquipmentSlotForCategory: vi.fn((cat: number) => cat - 1),
	inventoryManager: {
		get unlockedItemList() {
			return [];
		},
		equippedSlots: [],
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

import Inventory from '$routes/game/screens/inventory/Inventory.svelte';

afterEach(cleanup);

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
});
