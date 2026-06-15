import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EItemCategory, ERarity } from '$lib/api';
import type { Item } from '$lib/battle';

import GridSlot from '$routes/game/screens/inventory/GridSlot.svelte';

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
		equipped: false,
		favorite: false,
		equipmentSlotId: undefined,
		...overrides
	}) as unknown as Item;

describe('GridSlot — rendering', () => {
	it('renders the grid-slot element', () => {
		const { container } = render(GridSlot, { props: { item: makeItem() } });
		expect(container.querySelector('.grid-slot')).toBeTruthy();
	});

	it('shows the equipped-marker when the item is equipped', () => {
		const { container } = render(GridSlot, { props: { item: makeItem({ equipped: true }) } });
		expect(container.querySelector('.equipped-marker')).toBeTruthy();
	});

	it('does not show the equipped-marker when the item is not equipped', () => {
		const { container } = render(GridSlot, { props: { item: makeItem({ equipped: false }) } });
		expect(container.querySelector('.equipped-marker')).toBeNull();
	});

	it('shows the mod-count badge when the item has applied mods', () => {
		const item = makeItem({
			appliedMods: [{ id: 0, itemModSlotId: 0, attributes: [] } as unknown as Item['appliedMods'][0]]
		});
		const { container } = render(GridSlot, { props: { item } });
		expect(container.querySelector('.mod-count')).toBeTruthy();
	});

	it('does not show the mod-count badge when the item has no mods', () => {
		const { container } = render(GridSlot, { props: { item: makeItem({ appliedMods: [] }) } });
		expect(container.querySelector('.mod-count')).toBeNull();
	});

	it('applies the "selected" class when selected=true', () => {
		const { container } = render(GridSlot, { props: { item: makeItem(), selected: true } });
		expect(container.querySelector('.grid-slot')!.classList.contains('selected')).toBe(true);
	});

	it('does not apply the "selected" class when selected=false', () => {
		const { container } = render(GridSlot, { props: { item: makeItem(), selected: false } });
		expect(container.querySelector('.grid-slot')!.classList.contains('selected')).toBe(false);
	});

	it('marks the fav-star as "on" when the item is a favorite', () => {
		const { container } = render(GridSlot, { props: { item: makeItem({ favorite: true }) } });
		expect(container.querySelector('.fav-star')!.classList.contains('on')).toBe(true);
	});

	it('does not mark the fav-star as "on" for a non-favorite item', () => {
		const { container } = render(GridSlot, { props: { item: makeItem({ favorite: false }) } });
		expect(container.querySelector('.fav-star')!.classList.contains('on')).toBe(false);
	});
});

describe('GridSlot — click interactions', () => {
	it('calls onSelect with the item on a plain click', async () => {
		const onSelect = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect } });
		await fireEvent.click(container.querySelector('.grid-slot')!);
		expect(onSelect).toHaveBeenCalledWith(item);
	});

	it('calls onToggleEquip instead of onSelect on a ctrl+click', async () => {
		const onSelect = vi.fn();
		const onToggleEquip = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect, onToggleEquip } });
		await fireEvent.click(container.querySelector('.grid-slot')!, { ctrlKey: true });
		expect(onToggleEquip).toHaveBeenCalledWith(item);
		expect(onSelect).not.toHaveBeenCalled();
	});

	it('calls onToggleEquip instead of onSelect on a meta+click', async () => {
		const onSelect = vi.fn();
		const onToggleEquip = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect, onToggleEquip } });
		await fireEvent.click(container.querySelector('.grid-slot')!, { metaKey: true });
		expect(onToggleEquip).toHaveBeenCalledWith(item);
		expect(onSelect).not.toHaveBeenCalled();
	});

	it('calls onToggleEquip on a double-click', async () => {
		const onToggleEquip = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onToggleEquip } });
		await fireEvent.dblClick(container.querySelector('.grid-slot')!);
		expect(onToggleEquip).toHaveBeenCalledWith(item);
	});

	it('calls onToggleFav when the fav-star button is clicked and does not bubble to onSelect', async () => {
		const onToggleFav = vi.fn();
		const onSelect = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onToggleFav, onSelect } });
		await fireEvent.click(container.querySelector('.fav-star')!);
		expect(onToggleFav).toHaveBeenCalledWith(item);
		expect(onSelect).not.toHaveBeenCalled();
	});
});

describe('GridSlot — keyboard interactions', () => {
	it('calls onSelect on Enter key', async () => {
		const onSelect = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect } });
		await fireEvent.keyDown(container.querySelector('.grid-slot')!, { key: 'Enter' });
		expect(onSelect).toHaveBeenCalledWith(item);
	});

	it('calls onSelect on Space key', async () => {
		const onSelect = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect } });
		await fireEvent.keyDown(container.querySelector('.grid-slot')!, { key: ' ' });
		expect(onSelect).toHaveBeenCalledWith(item);
	});

	it('does not call onSelect for other keys', async () => {
		const onSelect = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect } });
		await fireEvent.keyDown(container.querySelector('.grid-slot')!, { key: 'ArrowRight' });
		expect(onSelect).not.toHaveBeenCalled();
	});

	it('equips (not selects) on Ctrl+Enter — the keyboard equivalent of ⌘/Ctrl-click', async () => {
		const onSelect = vi.fn();
		const onToggleEquip = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect, onToggleEquip } });
		await fireEvent.keyDown(container.querySelector('.grid-slot')!, { key: 'Enter', ctrlKey: true });
		expect(onToggleEquip).toHaveBeenCalledWith(item);
		expect(onSelect).not.toHaveBeenCalled();
	});

	it('equips (not selects) on Meta+Space', async () => {
		const onSelect = vi.fn();
		const onToggleEquip = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onSelect, onToggleEquip } });
		await fireEvent.keyDown(container.querySelector('.grid-slot')!, { key: ' ', metaKey: true });
		expect(onToggleEquip).toHaveBeenCalledWith(item);
		expect(onSelect).not.toHaveBeenCalled();
	});
});

describe('GridSlot — hover interactions', () => {
	it('shows the fav-star (adds "show" class) on mouse enter', async () => {
		const { container } = render(GridSlot, { props: { item: makeItem() } });
		await fireEvent.mouseEnter(container.querySelector('.grid-slot')!);
		expect(container.querySelector('.fav-star')!.classList.contains('show')).toBe(true);
	});

	it('hides the fav-star ("show" removed) on mouse leave', async () => {
		const { container } = render(GridSlot, { props: { item: makeItem() } });
		await fireEvent.mouseEnter(container.querySelector('.grid-slot')!);
		await fireEvent.mouseLeave(container.querySelector('.grid-slot')!);
		expect(container.querySelector('.fav-star')!.classList.contains('show')).toBe(false);
	});

	it('calls onHoverEnter with the item and event on mouse enter', async () => {
		const onHoverEnter = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onHoverEnter } });
		await fireEvent.mouseEnter(container.querySelector('.grid-slot')!);
		expect(onHoverEnter).toHaveBeenCalledWith(item, expect.any(MouseEvent));
	});

	it('calls onHoverLeave on mouse leave', async () => {
		const onHoverLeave = vi.fn();
		const { container } = render(GridSlot, { props: { item: makeItem(), onHoverLeave } });
		await fireEvent.mouseLeave(container.querySelector('.grid-slot')!);
		expect(onHoverLeave).toHaveBeenCalled();
	});

	it('calls onHoverMove with the event on mouse move', async () => {
		const onHoverMove = vi.fn();
		const { container } = render(GridSlot, { props: { item: makeItem(), onHoverMove } });
		await fireEvent.mouseMove(container.querySelector('.grid-slot')!);
		expect(onHoverMove).toHaveBeenCalledWith(expect.any(MouseEvent));
	});
});

describe('GridSlot — drag interactions', () => {
	it('sets dataTransfer text/plain to the itemId string on drag start', async () => {
		const item = makeItem({ itemId: 42 });
		const { container } = render(GridSlot, { props: { item } });
		const dt = { setData: vi.fn(), effectAllowed: '' };
		await fireEvent.dragStart(container.querySelector('.grid-slot')!, { dataTransfer: dt });
		expect(dt.setData).toHaveBeenCalledWith('text/plain', '42');
	});

	it('sets dataTransfer.effectAllowed to "move" on drag start', async () => {
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item } });
		const dt = { setData: vi.fn(), effectAllowed: '' };
		await fireEvent.dragStart(container.querySelector('.grid-slot')!, { dataTransfer: dt });
		expect(dt.effectAllowed).toBe('move');
	});

	it('calls onDragStart with the item on drag start', async () => {
		const onDragStart = vi.fn();
		const item = makeItem();
		const { container } = render(GridSlot, { props: { item, onDragStart } });
		await fireEvent.dragStart(container.querySelector('.grid-slot')!, {
			dataTransfer: { setData: vi.fn(), effectAllowed: '' }
		});
		expect(onDragStart).toHaveBeenCalledWith(item);
	});

	it('calls onDragEnd on drag end', async () => {
		const onDragEnd = vi.fn();
		const { container } = render(GridSlot, { props: { item: makeItem(), onDragEnd } });
		await fireEvent.dragEnd(container.querySelector('.grid-slot')!);
		expect(onDragEnd).toHaveBeenCalled();
	});
});
