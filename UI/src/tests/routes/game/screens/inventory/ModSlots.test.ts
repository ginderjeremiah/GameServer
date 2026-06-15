import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EItemCategory, EItemModType, ERarity } from '$lib/api';
import type { Item, ItemMod } from '$lib/battle';
import type { InventoryView } from '$routes/game/screens/inventory/inventory-view.svelte';

import ModSlots from '$routes/game/screens/inventory/ModSlots.svelte';

afterEach(cleanup);

const makeModSlot = (id: number, typeId: EItemModType = EItemModType.Component) => ({
	id,
	itemId: 0,
	itemModSlotTypeId: typeId
});

const makeMod = (id: number, slotId: number, overrides: Partial<ItemMod> = {}): ItemMod =>
	({
		id,
		itemModSlotId: slotId,
		name: `Mod ${id}`,
		description: `Mod ${id} description`,
		rarityId: ERarity.Common,
		itemModTypeId: EItemModType.Component,
		attributes: [],
		...overrides
	}) as unknown as ItemMod;

const makeItem = (overrides: Partial<Item> = {}): Item =>
	({
		id: 1,
		itemId: 1,
		name: 'Test Item',
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
		...overrides
	}) as unknown as Item;

const makeView = (overrides: Partial<InventoryView> = {}): InventoryView =>
	({
		compatibleMods: vi.fn(() => []),
		applyMod: vi.fn(),
		removeMod: vi.fn(),
		...overrides
	}) as unknown as InventoryView;

describe('ModSlots — no slots', () => {
	it('shows the "no mod slots" message when the item has no modSlots', () => {
		render(ModSlots, { props: { item: makeItem({ modSlots: [] }), view: makeView() } });
		expect(screen.getByText('This item has no mod slots.')).toBeTruthy();
	});

	it('does not render any mod slot elements when the item has no slots', () => {
		const { container } = render(ModSlots, { props: { item: makeItem({ modSlots: [] }), view: makeView() } });
		expect(container.querySelector('.mod-slot')).toBeNull();
	});
});

describe('ModSlots — empty slots', () => {
	it('renders one slot row per mod slot', () => {
		const item = makeItem({ modSlots: [makeModSlot(1), makeModSlot(2)] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		expect(container.querySelectorAll('.mod-slot').length).toBe(2);
	});

	it('shows the "Empty slot" label for an unfilled slot', () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		render(ModSlots, { props: { item, view: makeView() } });
		expect(screen.getByText('Empty slot')).toBeTruthy();
	});

	it('shows the click hint for an unfilled slot', () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		render(ModSlots, { props: { item, view: makeView() } });
		expect(screen.getByText(/Click to install a/i)).toBeTruthy();
	});

	it('renders an empty slot as a real <button> (keyboard-operable, no hand-rolled keydown)', () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		const slot = container.querySelector('.mod-slot') as HTMLElement;
		expect(slot.tagName).toBe('BUTTON');
	});
});

describe('ModSlots — picker toggle', () => {
	it('opens the picker panel when an empty slot is clicked', async () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		await fireEvent.click(container.querySelector('.mod-slot')!);
		expect(container.querySelector('.mod-picker')).toBeTruthy();
	});

	it('closes the picker when the same empty slot is clicked again', async () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		await fireEvent.click(container.querySelector('.mod-slot')!);
		await fireEvent.click(container.querySelector('.mod-slot')!);
		expect(container.querySelector('.mod-picker')).toBeNull();
	});

	it('shows "No unlocked … mods available" when compatibleMods returns empty', async () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		const view = makeView({ compatibleMods: vi.fn(() => []) });
		const { container } = render(ModSlots, { props: { item, view } });
		await fireEvent.click(container.querySelector('.mod-slot')!);
		expect(container.querySelector('.picker-empty')).toBeTruthy();
	});

	it('renders picker options when compatible mods are available', async () => {
		const item = makeItem({ modSlots: [makeModSlot(1)] });
		const mods = [makeMod(10, 1), makeMod(11, 1)];
		const view = makeView({ compatibleMods: vi.fn(() => mods) });
		const { container } = render(ModSlots, { props: { item, view } });
		await fireEvent.click(container.querySelector('.mod-slot')!);
		expect(container.querySelectorAll('.picker-option').length).toBe(2);
	});
});

describe('ModSlots — mod installation', () => {
	it('calls view.applyMod and closes the picker when a picker option is clicked', async () => {
		const item = makeItem({ modSlots: [makeModSlot(1)], itemId: 42 });
		const mods = [makeMod(10, 1)];
		const view = makeView({ compatibleMods: vi.fn(() => mods) });
		const { container } = render(ModSlots, { props: { item, view } });
		await fireEvent.click(container.querySelector('.mod-slot')!);
		await fireEvent.click(container.querySelector('.picker-option')!);
		expect(view.applyMod).toHaveBeenCalledWith(42, 1, 10);
		expect(container.querySelector('.mod-picker')).toBeNull();
	});
});

describe('ModSlots — filled slots', () => {
	it('shows the applied mod name on a filled slot', () => {
		const slot = makeModSlot(1);
		const mod = makeMod(10, 1, { name: 'Power Core' });
		const item = makeItem({ modSlots: [slot], appliedMods: [mod as unknown as Item['appliedMods'][0]] });
		render(ModSlots, { props: { item, view: makeView() } });
		expect(screen.getByText('Power Core')).toBeTruthy();
	});

	it('does not open the picker when a filled slot is clicked', async () => {
		const slot = makeModSlot(1);
		const mod = makeMod(10, 1);
		const item = makeItem({ modSlots: [slot], appliedMods: [mod as unknown as Item['appliedMods'][0]] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		await fireEvent.click(container.querySelector('.mod-slot')!);
		expect(container.querySelector('.mod-picker')).toBeNull();
	});

	it('shows the remove button (×) on a filled slot', () => {
		const slot = makeModSlot(1);
		const mod = makeMod(10, 1);
		const item = makeItem({ modSlots: [slot], appliedMods: [mod as unknown as Item['appliedMods'][0]] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		expect(container.querySelector('.mod-remove')).toBeTruthy();
	});

	it('renders a filled slot as a non-interactive <div> (it carries its own remove button)', () => {
		const slot = makeModSlot(1);
		const mod = makeMod(10, 1);
		const item = makeItem({ modSlots: [slot], appliedMods: [mod as unknown as Item['appliedMods'][0]] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		// A filled slot must not be a button (no nested button-in-button), unlike the empty slot.
		expect((container.querySelector('.mod-slot') as HTMLElement).tagName).toBe('DIV');
	});

	it('labels the remove button with the applied mod name', () => {
		const slot = makeModSlot(1);
		const mod = makeMod(10, 1, { name: 'Power Core' });
		const item = makeItem({ modSlots: [slot], appliedMods: [mod as unknown as Item['appliedMods'][0]] });
		const { container } = render(ModSlots, { props: { item, view: makeView() } });
		expect((container.querySelector('.mod-remove') as HTMLElement).getAttribute('aria-label')).toBe(
			'Remove Power Core'
		);
	});

	it('calls view.removeMod with itemId and slotId when the remove button is clicked', async () => {
		const slot = makeModSlot(1);
		const mod = makeMod(10, 1);
		const item = makeItem({ modSlots: [slot], appliedMods: [mod as unknown as Item['appliedMods'][0]], itemId: 42 });
		const view = makeView();
		const { container } = render(ModSlots, { props: { item, view } });
		await fireEvent.click(container.querySelector('.mod-remove')!);
		expect(view.removeMod).toHaveBeenCalledWith(42, 1);
	});
});
