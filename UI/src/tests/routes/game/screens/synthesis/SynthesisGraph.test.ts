import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import SynthesisGraph from '$routes/game/screens/synthesis/SynthesisGraph.svelte';
import type { SynthesisGraphLayout } from '$routes/game/screens/synthesis/synthesis-graph';

/* Interaction contract of the graph canvas (issue #1505): the recenter effect keys on the content extent
   (not the layout object identity, which changes on every proficiency-XP push), the pointer is captured
   only once a real drag starts (so node/reset clicks aren't retargeted away), and wheel zoom runs through
   a manual non-passive listener. jsdom can't assert passive-ness itself — that part is browser-only. */

// jsdom lacks the pointer-capture API; install no-ops so the component's calls are observable via spies.
if (typeof Element.prototype.setPointerCapture !== 'function') {
	Element.prototype.setPointerCapture = () => {};
}
if (typeof Element.prototype.releasePointerCapture !== 'function') {
	Element.prototype.releasePointerCapture = () => {};
}
if (typeof Element.prototype.hasPointerCapture !== 'function') {
	Element.prototype.hasPointerCapture = () => false;
}
const setCapture = vi.spyOn(Element.prototype, 'setPointerCapture').mockImplementation(() => {});
vi.spyOn(Element.prototype, 'releasePointerCapture').mockImplementation(() => {});
vi.spyOn(Element.prototype, 'hasPointerCapture').mockImplementation(() => false);

/** A minimal two-node layout; width/height drive the recenter math, so tests control them directly. */
const makeLayout = (width: number, height: number): SynthesisGraphLayout => ({
	nodes: [
		{ key: 'f0', kind: 'fusion', x: 40, y: 40, recipeId: 0, state: 'ready', masked: false, selectable: true },
		{ key: 'm0', kind: 'result', x: 120, y: 40, recipeId: 0, state: 'ready', masked: true, selectable: true }
	],
	edges: [{ key: 'f0->m0', from: 'f0', to: 'm0', state: 'ready' }],
	width,
	height
});

const setup = (layout: SynthesisGraphLayout = makeLayout(300, 200)) => {
	const onSelect = vi.fn();
	const rendered = render(SynthesisGraph, { props: { layout, selectedRecipeId: null, onSelect } });
	const canvas = rendered.getByTestId('synthesis-graph');
	const viewport = canvas.querySelector('.viewport') as HTMLElement;
	return { ...rendered, onSelect, canvas, viewport };
};

/** Drag the pointer on the canvas from `from` to `to` (primary button) without releasing. */
const drag = async (canvas: HTMLElement, from: [number, number], to: [number, number]) => {
	await fireEvent.pointerDown(canvas, { button: 0, pointerId: 1, clientX: from[0], clientY: from[1] });
	await fireEvent.pointerMove(canvas, { pointerId: 1, clientX: to[0], clientY: to[1] });
};

beforeEach(() => setCapture.mockClear());
afterEach(() => cleanup());

describe('SynthesisGraph — recenter vs pan', () => {
	// jsdom containers have zero clientWidth/Height, so recenter yields translate(-w/2, -h/2) at scale 1.
	it('recenters on mount from the layout extent', () => {
		const { viewport } = setup(makeLayout(300, 200));
		expect(viewport.style.transform).toBe('translate(-150px, -100px) scale(1)');
	});

	it('preserves the user pan when the layout is re-derived with the same extent', async () => {
		const { canvas, viewport, rerender } = setup(makeLayout(300, 200));
		await drag(canvas, [100, 100], [140, 120]);
		await fireEvent.pointerUp(canvas, { pointerId: 1 });
		expect(viewport.style.transform).toBe('translate(-110px, -80px) scale(1)');

		// A new layout object with the identical extent — the idle loop's XP-push re-derivation shape.
		await rerender({ layout: makeLayout(300, 200) });
		expect(viewport.style.transform).toBe('translate(-110px, -80px) scale(1)');
	});

	it('still recenters when the content extent actually changes', async () => {
		const { canvas, viewport, rerender } = setup(makeLayout(300, 200));
		await drag(canvas, [100, 100], [140, 120]);
		await fireEvent.pointerUp(canvas, { pointerId: 1 });

		await rerender({ layout: makeLayout(400, 300) });
		expect(viewport.style.transform).toBe('translate(-200px, -150px) scale(1)');
	});
});

describe('SynthesisGraph — pointer capture and node clicks', () => {
	it('does not capture the pointer on a plain press, so a node click still selects', async () => {
		const { canvas, viewport, onSelect, getByTestId } = setup();
		const node = getByTestId('graph-node-result-0');
		const before = viewport.style.transform;

		await fireEvent.pointerDown(node, { button: 0, pointerId: 1, clientX: 10, clientY: 10 });
		// Sub-threshold jitter stays a click: no capture, no pan.
		await fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 12, clientY: 11 });
		await fireEvent.pointerUp(canvas, { pointerId: 1 });
		await fireEvent.click(node);

		expect(setCapture).not.toHaveBeenCalled();
		expect(viewport.style.transform).toBe(before);
		expect(onSelect).toHaveBeenCalledWith(0);
	});

	it('captures the pointer only once the drag threshold is crossed', async () => {
		const { canvas } = setup();
		await fireEvent.pointerDown(canvas, { button: 0, pointerId: 1, clientX: 100, clientY: 100 });
		await fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 101, clientY: 100 });
		expect(setCapture).not.toHaveBeenCalled();

		await fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 110, clientY: 100 });
		expect(setCapture).toHaveBeenCalledTimes(1);
	});

	it('ignores non-primary-button presses entirely', async () => {
		const { canvas, viewport } = setup();
		const before = viewport.style.transform;

		await fireEvent.pointerDown(canvas, { button: 2, pointerId: 1, clientX: 100, clientY: 100 });
		await fireEvent.pointerMove(canvas, { pointerId: 1, clientX: 200, clientY: 180 });

		expect(setCapture).not.toHaveBeenCalled();
		expect(viewport.style.transform).toBe(before);
	});
});

describe('SynthesisGraph — wheel zoom', () => {
	it('zooms via the manually registered wheel listener and cancels the event', async () => {
		const { canvas, viewport } = setup();
		// fireEvent returns false when preventDefault was called — proof the handler cancels the scroll.
		const notPrevented = await fireEvent.wheel(canvas, { deltaY: -100, clientX: 50, clientY: 50 });
		expect(notPrevented).toBe(false);
		expect(viewport.style.transform).toContain('scale(1.1)');
	});
});
