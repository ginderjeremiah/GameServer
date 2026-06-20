import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import { tooltips, type TooltipData } from '$stores';
import type { Item } from '$lib/battle';
import type { ItemTooltipHandle } from '$routes/game/screens/inventory/item-tooltip.svelte';
import Fixture from './ItemTooltipControllerFixture.svelte';

afterEach(cleanup);

const makeItem = (itemId: number, name: string): Item => ({ itemId, name }) as unknown as Item;

// A pointer anchor resolves to its cursor coordinates; an element anchor would resolve off its box.
const anchorAt = (x: number, y: number) => ({ clientX: x, clientY: y }) as MouseEvent;

/** Renders the fixture and returns the live controller handle plus the tooltip store entry it owns. */
const mountController = (): { handle: ItemTooltipHandle; data: TooltipData } => {
	let handle: ItemTooltipHandle | undefined;
	render(Fixture, { props: { onHandle: (h: ItemTooltipHandle) => (handle = h) } });
	flushSync();
	if (!handle) {
		throw new Error('fixture did not surface the controller handle');
	}
	const data = [...tooltips.data].at(-1);
	if (!data) {
		throw new Error('controller did not register a tooltip');
	}
	return { handle, data };
};

describe('createItemTooltip', () => {
	it('registers a tooltip on mount and unregisters it on destroy', () => {
		const before = [...tooltips.data].length;
		const view = render(Fixture, { props: { onHandle: () => {} } });
		flushSync();
		expect([...tooltips.data].length).toBe(before + 1);

		view.unmount();
		flushSync();
		expect([...tooltips.data].length).toBe(before);
	});

	it('exposes a stable describedById for aria-describedby wiring', () => {
		const { handle, data } = mountController();
		expect(handle.controller.describedById).toBe(`tooltip-${data.id}`);
	});

	it('starts hidden with no item', () => {
		const { handle, data } = mountController();
		expect(handle.item).toBeUndefined();
		expect(data.visible).toBe(false);
	});

	it('show sets the item, positions the panel, and reveals it', () => {
		const { handle, data } = mountController();
		const item = makeItem(7, 'Alpha Blade');

		handle.controller.show(item, anchorAt(120, 45));
		flushSync();

		// `$state` deep-proxies the stored object, so compare by value rather than reference identity.
		expect(handle.item?.itemId).toBe(7);
		expect(handle.item?.name).toBe('Alpha Blade');
		expect(data.position).toEqual({ x: 120, y: 45 });
		expect(data.visible).toBe(true);
	});

	it('move repositions without clearing the shown item', () => {
		const { handle, data } = mountController();
		const item = makeItem(7, 'Alpha Blade');
		handle.controller.show(item, anchorAt(120, 45));
		flushSync();

		handle.controller.move(anchorAt(200, 90));
		flushSync();

		// move only repositions the panel; the shown item is left intact.
		expect(data.position).toEqual({ x: 200, y: 90 });
		expect(handle.item?.itemId).toBe(7);
		expect(data.visible).toBe(true);
	});

	it('hide clears the item and conceals the panel', () => {
		const { handle, data } = mountController();
		handle.controller.show(makeItem(7, 'Alpha Blade'), anchorAt(120, 45));
		flushSync();

		handle.controller.hide();
		flushSync();

		expect(handle.item).toBeUndefined();
		expect(data.visible).toBe(false);
	});
});
