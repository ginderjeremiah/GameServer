import { describe, it, expect, beforeEach, afterEach, vi, type Mock } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import type { AttributesView } from '$routes/game/screens/attributes/attributes-view.svelte';

import AttributesRadar from '$routes/game/screens/attributes/AttributesRadar.svelte';

const makeView = (
	overrides: Partial<{
		canInc: () => boolean;
		inc: (i: number) => void;
		dec: (i: number) => void;
		setValue: (i: number, v: number) => void;
		lockScale: () => void;
		unlockScale: () => void;
	}>
): AttributesView =>
	({
		values: [5, 5, 5, 5, 5, 5],
		savedValues: [5, 5, 5, 5, 5, 5],
		hexMax: 20,
		canInc: vi.fn(() => true),
		inc: vi.fn(),
		dec: vi.fn(),
		setValue: vi.fn(),
		lockScale: vi.fn(),
		unlockScale: vi.fn(),
		...overrides
	}) as unknown as AttributesView;

beforeEach(() => {
	// Force the reduced-motion path so rAF never runs during tests.
	window.matchMedia = vi.fn().mockReturnValue({ matches: true }) as unknown as typeof window.matchMedia;
});

afterEach(cleanup);

describe('AttributesRadar — rendering', () => {
	it('renders an SVG radar element', () => {
		const { container } = render(AttributesRadar, { props: { view: makeView({}) } });
		expect(container.querySelector('svg.radar')).toBeTruthy();
	});

	it('renders one interactive vertex button per core attribute (6 total)', () => {
		const { container } = render(AttributesRadar, { props: { view: makeView({}) } });
		expect(container.querySelectorAll('[role="button"]').length).toBe(6);
	});
});

describe('AttributesRadar — click interaction', () => {
	it('calls view.inc(0) when the first vertex is clicked and canInc() is true', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		await fireEvent.click(container.querySelectorAll('[role="button"]')[0]);
		expect(view.inc).toHaveBeenCalledWith(0);
	});

	it('calls view.inc(i) with the correct index for each vertex', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertices = container.querySelectorAll('[role="button"]');
		for (let i = 0; i < 6; i++) {
			await fireEvent.click(vertices[i]);
		}
		expect(view.inc).toHaveBeenCalledTimes(6);
		for (let i = 0; i < 6; i++) {
			expect(view.inc).toHaveBeenCalledWith(i);
		}
	});

	it('does not call view.inc when canInc() returns false', async () => {
		const view = makeView({ canInc: vi.fn(() => false) });
		const { container } = render(AttributesRadar, { props: { view } });
		await fireEvent.click(container.querySelectorAll('[role="button"]')[0]);
		expect(view.inc).not.toHaveBeenCalled();
	});

	it('does not call view.inc when interactive=false', async () => {
		const view = makeView({ canInc: vi.fn(() => true) });
		const { container } = render(AttributesRadar, { props: { view, interactive: false } });
		await fireEvent.click(container.querySelectorAll('[role="button"]')[0]);
		expect(view.inc).not.toHaveBeenCalled();
	});
});

describe('AttributesRadar — drag interaction', () => {
	// The default radar (size 430) has centre 215 and axis length 155 (215 − 60),
	// with the first axis pointing straight up. A pointer at (215, 215 − 77.5)
	// projects to half the rim → hexMax/2 = 10.
	const C = 215;
	const mockRect = (svg: Element): void => {
		vi.spyOn(svg, 'getBoundingClientRect').mockReturnValue({
			left: 0,
			top: 0,
			width: 430,
			height: 430,
			right: 430,
			bottom: 430,
			x: 0,
			y: 0,
			toJSON: () => ({})
		} as DOMRect);
	};

	const setup = (overrides: Parameters<typeof makeView>[0] = {}, props: Record<string, unknown> = {}) => {
		const view = makeView(overrides);
		const { container } = render(AttributesRadar, { props: { view, ...props } });
		const svg = container.querySelector('svg.radar');
		expect(svg).toBeTruthy();
		mockRect(svg as Element);
		const vertices = container.querySelectorAll('[role="button"]');
		return { view, vertices };
	};

	it('drags a vertex outward and sets the projected value via setValue', async () => {
		const { view, vertices } = setup();
		await fireEvent.pointerDown(vertices[0]);
		await fireEvent.pointerMove(window, { clientX: C, clientY: C - 77.5 });

		expect(view.setValue).toHaveBeenCalledTimes(1);
		const [axis, value] = (view.setValue as Mock).mock.calls[0];
		expect(axis).toBe(0);
		expect(value).toBeCloseTo(10, 5);
	});

	it('treats a press without movement as the quick +1 click', async () => {
		const { view, vertices } = setup();
		await fireEvent.pointerDown(vertices[0]);
		await fireEvent.pointerUp(window);
		await fireEvent.click(vertices[0]);

		expect(view.inc).toHaveBeenCalledWith(0);
		expect(view.setValue).not.toHaveBeenCalled();
	});

	it('keeps a jittery tap as a +1 click when movement stays within the dead-zone', async () => {
		const { view, vertices } = setup();
		// Press at a known point, then jitter a couple of pixels — under the 5px
		// dead-zone, so it must remain a tap, not a (no-op/refunding) drag.
		await fireEvent.pointerDown(vertices[0], { clientX: C, clientY: 60 });
		await fireEvent.pointerMove(window, { clientX: C + 2, clientY: 61 });
		await fireEvent.pointerUp(window);
		await fireEvent.click(vertices[0]);

		expect(view.setValue).not.toHaveBeenCalled();
		expect(view.inc).toHaveBeenCalledWith(0);
	});

	it('suppresses the synthetic click that follows a drag', async () => {
		const { view, vertices } = setup();
		await fireEvent.pointerDown(vertices[0]);
		await fireEvent.pointerMove(window, { clientX: C, clientY: C - 50 });
		await fireEvent.pointerUp(window);
		await fireEvent.click(vertices[0]);

		expect(view.setValue).toHaveBeenCalled();
		expect(view.inc).not.toHaveBeenCalled();
	});

	it('allows dragging inward to refund even when canInc() is false', async () => {
		const { view, vertices } = setup({ canInc: vi.fn(() => false) });
		await fireEvent.pointerDown(vertices[0]);
		// Inward, toward the centre — a smaller allocation.
		await fireEvent.pointerMove(window, { clientX: C, clientY: C - 31 });

		expect(view.setValue).toHaveBeenCalledTimes(1);
		const [axis, value] = (view.setValue as Mock).mock.calls[0];
		expect(axis).toBe(0);
		expect(value).toBeCloseTo(4, 5);
	});

	it('does not start a drag when interactive=false', async () => {
		const { view, vertices } = setup({}, { interactive: false });
		await fireEvent.pointerDown(vertices[0]);
		await fireEvent.pointerMove(window, { clientX: C, clientY: C - 77.5 });

		expect(view.setValue).not.toHaveBeenCalled();
	});

	it('pins the radar scale on drag start and releases it on pointer up', async () => {
		// Pinning the scale for the gesture stops a mid-drag rescale from inflating
		// the value under the pointer near the boundary values (#433).
		const { view, vertices } = setup();
		await fireEvent.pointerDown(vertices[0]);
		expect(view.lockScale).toHaveBeenCalledTimes(1);
		expect(view.unlockScale).not.toHaveBeenCalled();

		await fireEvent.pointerMove(window, { clientX: C, clientY: C - 77.5 });
		await fireEvent.pointerUp(window);
		expect(view.unlockScale).toHaveBeenCalledTimes(1);
	});

	it('releases the pinned scale on pointer cancel', async () => {
		const { view, vertices } = setup();
		await fireEvent.pointerDown(vertices[0]);
		await fireEvent.pointerCancel(window);
		expect(view.unlockScale).toHaveBeenCalledTimes(1);
	});

	it('does not pin the scale when interactive=false', async () => {
		const { view, vertices } = setup({}, { interactive: false });
		await fireEvent.pointerDown(vertices[0]);
		await fireEvent.pointerUp(window);
		expect(view.lockScale).not.toHaveBeenCalled();
		expect(view.unlockScale).not.toHaveBeenCalled();
	});
});

describe('AttributesRadar — keyboard interaction', () => {
	it('calls view.inc(i) when Enter is pressed on a vertex', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertex = container.querySelectorAll('[role="button"]')[0];
		await fireEvent.keyDown(vertex, { key: 'Enter' });
		expect(view.inc).toHaveBeenCalledWith(0);
	});

	it('calls view.inc(i) when Space is pressed on a vertex', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertex = container.querySelectorAll('[role="button"]')[1];
		await fireEvent.keyDown(vertex, { key: ' ' });
		expect(view.inc).toHaveBeenCalledWith(1);
	});

	it('calls view.inc(i) when ArrowUp or ArrowRight is pressed on a vertex', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertices = container.querySelectorAll('[role="button"]');
		await fireEvent.keyDown(vertices[0], { key: 'ArrowUp' });
		await fireEvent.keyDown(vertices[1], { key: 'ArrowRight' });
		expect(view.inc).toHaveBeenCalledWith(0);
		expect(view.inc).toHaveBeenCalledWith(1);
	});

	it('calls view.dec(i) when ArrowDown or ArrowLeft is pressed on a vertex', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertices = container.querySelectorAll('[role="button"]');
		await fireEvent.keyDown(vertices[2], { key: 'ArrowDown' });
		await fireEvent.keyDown(vertices[3], { key: 'ArrowLeft' });
		expect(view.dec).toHaveBeenCalledWith(2);
		expect(view.dec).toHaveBeenCalledWith(3);
	});

	it('still refunds via the arrow keys when canInc() is false (at max budget)', async () => {
		const view = makeView({ canInc: vi.fn(() => false) });
		const { container } = render(AttributesRadar, { props: { view } });
		await fireEvent.keyDown(container.querySelectorAll('[role="button"]')[0], { key: 'ArrowDown' });
		expect(view.dec).toHaveBeenCalledWith(0);
	});

	it('does not call inc or dec when a non-activation key is pressed', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertex = container.querySelectorAll('[role="button"]')[0];
		await fireEvent.keyDown(vertex, { key: 'Tab' });
		expect(view.inc).not.toHaveBeenCalled();
		expect(view.dec).not.toHaveBeenCalled();
	});

	it('does not handle keys when interactive=false', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view, interactive: false } });
		const vertex = container.querySelectorAll('[role="button"]')[0];
		await fireEvent.keyDown(vertex, { key: 'ArrowDown' });
		await fireEvent.keyDown(vertex, { key: 'Enter' });
		expect(view.inc).not.toHaveBeenCalled();
		expect(view.dec).not.toHaveBeenCalled();
	});
});

describe('AttributesRadar — tabindex', () => {
	it('keeps all vertices focusable (tabindex=0) when interactive, even at max budget', () => {
		// Focusable regardless of canInc so a keyboard user can refund points when the budget is spent.
		const view = makeView({ canInc: vi.fn(() => false) });
		const { container } = render(AttributesRadar, { props: { view } });
		const vertices = container.querySelectorAll('[role="button"]');
		vertices.forEach((v) => expect(v.getAttribute('tabindex')).toBe('0'));
	});

	it('sets tabindex=-1 on all vertices when interactive=false', () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view, interactive: false } });
		const vertices = container.querySelectorAll('[role="button"]');
		vertices.forEach((v) => expect(v.getAttribute('tabindex')).toBe('-1'));
	});
});
