import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EItemCategory, ERarity } from '$lib/api';
import type { Item } from '$lib/battle';
import { type EquipSlotDef } from '$routes/game/screens/inventory/inventory-view.svelte';

import EquipSlot from '$routes/game/screens/inventory/EquipSlot.svelte';

afterEach(cleanup);

// EquipSlotDef uses EEquipmentSlot as the id type — we use a plain number since
// the component only reads it opaquely to pass back via onDrop/onUnequip.
const helmSlot: EquipSlotDef = {
	id: 0 as EquipSlotDef['id'],
	label: 'Helm',
	category: EItemCategory.Helm,
	group: 'armor'
};

const makeItem = (overrides: Partial<Item> = {}): Item =>
	({
		id: 1,
		itemId: 1,
		name: 'Iron Helm',
		description: 'A sturdy helm.',
		itemCategoryId: EItemCategory.Helm,
		rarityId: ERarity.Common,
		iconPath: '',
		attributes: [],
		appliedMods: [],
		modSlots: [],
		tags: [],
		equipped: true,
		favorite: false,
		equipmentSlotId: 0,
		...overrides
	}) as unknown as Item;

describe('EquipSlot — empty state', () => {
	it('shows the slot label', () => {
		render(EquipSlot, { props: { slot: helmSlot } });
		expect(screen.getByText('Helm')).toBeTruthy();
	});

	it('shows the "Empty" label when no item is equipped', () => {
		render(EquipSlot, { props: { slot: helmSlot } });
		expect(screen.getByText('Empty')).toBeTruthy();
	});

	it('does not have the "filled" class on the tile when empty', () => {
		const { container } = render(EquipSlot, { props: { slot: helmSlot } });
		expect(container.querySelector('.equip-tile')!.classList.contains('filled')).toBe(false);
	});
});

describe('EquipSlot — filled state', () => {
	it('shows the item name when filled', () => {
		render(EquipSlot, { props: { slot: helmSlot, item: makeItem() } });
		expect(screen.getByText('Iron Helm')).toBeTruthy();
	});

	it('has the "filled" class on the tile when an item is equipped', () => {
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item: makeItem() } });
		expect(container.querySelector('.equip-tile')!.classList.contains('filled')).toBe(true);
	});

	it('shows the mod count badge when the item has mods', () => {
		const item = makeItem({ appliedMods: [{ id: 1 } as unknown as Item['appliedMods'][0]] });
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item } });
		expect(container.querySelector('.mod-count')).toBeTruthy();
	});

	it('does not show the mod count badge when the item has no mods', () => {
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item: makeItem() } });
		expect(container.querySelector('.mod-count')).toBeNull();
	});
});

describe('EquipSlot — interactions', () => {
	it('calls onSelect with the item when the filled tile is clicked', async () => {
		const onSelect = vi.fn();
		const item = makeItem();
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item, onSelect } });
		await fireEvent.click(container.querySelector('.equip-tile')!);
		expect(onSelect).toHaveBeenCalledWith(item);
	});

	it('does not call onSelect when the empty tile is clicked', async () => {
		const onSelect = vi.fn();
		const { container } = render(EquipSlot, { props: { slot: helmSlot, onSelect } });
		await fireEvent.click(container.querySelector('.equip-tile')!);
		expect(onSelect).not.toHaveBeenCalled();
	});

	it('shows the unequip button on mouse enter', async () => {
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item: makeItem() } });
		await fireEvent.mouseEnter(container.querySelector('.equip-tile')!);
		expect(container.querySelector('.unequip')).toBeTruthy();
	});

	it('hides the unequip button after mouse leave', async () => {
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item: makeItem() } });
		await fireEvent.mouseEnter(container.querySelector('.equip-tile')!);
		await fireEvent.mouseLeave(container.querySelector('.equip-tile')!);
		expect(container.querySelector('.unequip')).toBeNull();
	});

	it('calls onUnequip with the slot id when the unequip button is clicked', async () => {
		const onUnequip = vi.fn();
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item: makeItem(), onUnequip } });
		await fireEvent.mouseEnter(container.querySelector('.equip-tile')!);
		await fireEvent.click(container.querySelector('.unequip')!);
		expect(onUnequip).toHaveBeenCalledWith(helmSlot.id);
	});

	it('marks the tile as "selected" when selected=true', () => {
		const { container } = render(EquipSlot, { props: { slot: helmSlot, item: makeItem(), selected: true } });
		expect(container.querySelector('.equip-tile')!.classList.contains('selected')).toBe(true);
	});
});

describe('EquipSlot — drag and drop', () => {
	it('marks the tile "can-accept" when a dragItem has a matching category', () => {
		const dragItem = makeItem({ itemCategoryId: EItemCategory.Helm });
		const { container } = render(EquipSlot, { props: { slot: helmSlot, dragItem } });
		expect(container.querySelector('.equip-tile')!.classList.contains('can-accept')).toBe(true);
	});

	it('does not mark the tile "can-accept" for a mismatched category', () => {
		const dragItem = makeItem({ itemCategoryId: EItemCategory.Weapon });
		const { container } = render(EquipSlot, { props: { slot: helmSlot, dragItem } });
		expect(container.querySelector('.equip-tile')!.classList.contains('can-accept')).toBe(false);
	});

	it('calls onDrop with the slot id when a compatible item is dropped', async () => {
		const onDrop = vi.fn();
		const dragItem = makeItem({ itemCategoryId: EItemCategory.Helm });
		const { container } = render(EquipSlot, { props: { slot: helmSlot, dragItem, onDrop } });
		await fireEvent.drop(container.querySelector('.equip-tile')!);
		expect(onDrop).toHaveBeenCalledWith(helmSlot.id);
	});
});
