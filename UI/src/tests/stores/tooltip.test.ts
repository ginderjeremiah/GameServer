import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import { anchorPosition, tooltipElementId, tooltips } from '$stores/tooltip.svelte';
import Registrar from './TooltipRegistrarFixture.svelte';
import Consumer from './TooltipConsumerFixture.svelte';
import Nav from './TooltipNavFixture.svelte';
import DescribedBy from './TooltipDescribedByFixture.svelte';

afterEach(() => {
	cleanup();
});

describe('tooltip store', () => {
	it('registers a tooltip on mount and removes it on destroy', () => {
		const a = render(Registrar);
		expect([...tooltips.data]).toHaveLength(1);

		a.unmount();
		expect([...tooltips.data]).toHaveLength(0);
	});

	it('keeps the correct entries when screens overlap during navigation', () => {
		// A consumer (like TooltipBase) is actively rendering the reactive
		// collection while screens mount and unmount.
		render(Consumer);

		// Simulate navigating Fight -> Inventory: the new screen's tooltips mount
		// before the old screen's tooltips are destroyed.
		const fightA = render(Registrar);
		const fightB = render(Registrar);
		flushSync();
		const fightIds = [...tooltips.data].map((t) => t.id);
		expect(fightIds).toHaveLength(2);

		const invC = render(Registrar);
		const invD = render(Registrar);
		flushSync();
		expect([...tooltips.data]).toHaveLength(4);
		const invIds = [...tooltips.data].map((t) => t.id).filter((id) => !fightIds.includes(id));
		expect(invIds).toHaveLength(2);

		// Old (Fight) screen tears down after the new screen has mounted.
		fightA.unmount();
		fightB.unmount();
		flushSync();

		const remaining = [...tooltips.data];
		// No undefined holes should leak through.
		expect(remaining.every((t) => t !== undefined)).toBe(true);
		// Exactly the two inventory tooltips should remain.
		expect(remaining).toHaveLength(2);
		expect(remaining.map((t) => t.id).sort()).toEqual(invIds.sort());

		invC.unmount();
		invD.unmount();
		expect([...tooltips.data]).toHaveLength(0);
	});

	it('swapping screens within a single tree keeps exactly the new screen tooltips', async () => {
		const view = render(Nav, { props: { screen: 'fight' as const } });
		flushSync();
		expect([...tooltips.data]).toHaveLength(2);

		await view.rerender({ screen: 'inventory' as const });
		flushSync();

		expect([...tooltips.data].every((t) => t !== undefined)).toBe(true);
		expect([...tooltips.data]).toHaveLength(2);
	});

	it('returns a describedById matching its container id, for aria-describedby wiring', () => {
		const { getByTestId } = render(DescribedBy);
		flushSync();

		const [data] = [...tooltips.data];
		expect(data).toBeDefined();
		// The trigger's aria-describedby points at the container the renderer gives this id.
		expect(getByTestId('described-by-id').textContent).toBe(tooltipElementId(data.id));
	});
});

describe('tooltipElementId', () => {
	it('derives a stable container id from a numeric tooltip id', () => {
		expect(tooltipElementId(42)).toBe('tooltip-42');
	});
});

describe('anchorPosition', () => {
	it('positions a pointer anchor at the cursor coordinates', () => {
		const ev = { clientX: 120, clientY: 45 } as MouseEvent;
		expect(anchorPosition(ev)).toEqual({ x: 120, y: 45 });
	});

	it('positions a focus anchor under the centre of the element box', () => {
		// Focus has no cursor, so the tooltip anchors off the element's rect instead.
		const el = document.createElement('button');
		el.getBoundingClientRect = () =>
			({
				left: 100,
				width: 40,
				bottom: 200,
				top: 180,
				right: 140,
				height: 20,
				x: 100,
				y: 180,
				toJSON: () => ({})
			}) as DOMRect;
		expect(anchorPosition(el)).toEqual({ x: 120, y: 200 });
	});
});
