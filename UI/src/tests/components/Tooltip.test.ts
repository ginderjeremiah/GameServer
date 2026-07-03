import { describe, it, expect, afterEach, beforeEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import Tooltip from '$components/Tooltip.svelte';

afterEach(cleanup);

interface TooltipProps {
	component: () => { getBaseNode: () => HTMLDivElement } | undefined;
	visible: boolean;
	position?: { x: number; y: number };
	id: number;
}

const makeProps = (overrides: Partial<TooltipProps> = {}): TooltipProps => ({
	id: 1,
	component: () => undefined,
	visible: false,
	position: undefined,
	...overrides
});

// Emulate real layout in jsdom (which always reports 0): a panel inside `display: none`
// measures 0x0 and only reports its real box once `display: block` has hit the DOM. This is
// the distinction the fix rests on — positioning must measure after the panel renders.
const stubLayout = (el: HTMLElement, width: number, height: number) => {
	Object.defineProperty(el, 'offsetWidth', {
		get: () => (el.style.display === 'block' ? width : 0),
		configurable: true
	});
	Object.defineProperty(el, 'offsetHeight', {
		get: () => (el.style.display === 'block' ? height : 0),
		configurable: true
	});
};

describe('Tooltip', () => {
	it('renders a container with role="tooltip"', () => {
		const { container } = render(Tooltip, { props: makeProps() });
		expect(container.querySelector('[role="tooltip"]')).toBeTruthy();
	});

	it('gives the container a stable id derived from the tooltip id (for aria-describedby)', () => {
		const { container } = render(Tooltip, { props: makeProps({ id: 7 }) });
		expect(container.querySelector('[role="tooltip"]')?.id).toBe('tooltip-7');
	});

	it('does not set display:block when not visible', () => {
		const { container } = render(Tooltip, { props: makeProps({ visible: false, position: { x: 50, y: 50 } }) });
		const tooltip = container.querySelector('[role="tooltip"]') as HTMLElement;
		expect(tooltip.style.display).not.toBe('block');
	});

	it('appends the component node into the tooltip container', () => {
		const node = document.createElement('div');
		node.textContent = 'tooltip-content';
		const props = makeProps({
			component: () => ({ getBaseNode: () => node }),
			visible: false
		});
		const { container } = render(Tooltip, { props });
		flushSync();
		// The $effect in Tooltip calls container.appendChild(comp.getBaseNode()),
		// so the node should be a child of the tooltip container.
		const tooltipEl = container.querySelector('[role="tooltip"]') as HTMLElement;
		expect(tooltipEl.querySelector('div')?.textContent).toBe('tooltip-content');
	});

	describe('position clamping', () => {
		beforeEach(() => {
			// Deterministic viewport so the clamp thresholds don't depend on the jsdom default.
			window.innerWidth = 1000;
			window.innerHeight = 800;
		});

		// Toggle `visible` after mount — mirroring the real store — so the positioning effect
		// re-runs with the bound container in place.
		const renderVisible = async (position: { x: number; y: number }) => {
			const props = makeProps({ visible: false, position });
			const result = render(Tooltip, { props });
			await result.rerender({ ...props, visible: true });
			flushSync();
			return result.container.querySelector('[role="tooltip"]') as HTMLElement;
		};

		it('anchors top-left near the origin (fits within the viewport)', async () => {
			const tooltip = await renderVisible({ x: 20, y: 30 });

			expect(tooltip.style.display).toBe('block');
			expect(tooltip.style.left).toBe('35px');
			expect(tooltip.style.top).toBe('45px');
			expect(tooltip.style.right).toBe('');
			expect(tooltip.style.bottom).toBe('');
		});

		it('flips to bottom-right when the cursor is near the far edges', async () => {
			const tooltip = await renderVisible({ x: 990, y: 795 });

			expect(tooltip.style.display).toBe('block');
			// right = innerWidth - x + 15 = 1000 - 990 + 15
			expect(tooltip.style.right).toBe('25px');
			// bottom = innerHeight - y + 15 = 800 - 795 + 15
			expect(tooltip.style.bottom).toBe('20px');
			expect(tooltip.style.left).toBe('');
			expect(tooltip.style.top).toBe('');
		});
	});

	// The first show is the only positioning a keyboard-focus trigger gets (hover self-heals via
	// mousemove), so the edge flip must already see the panel's real size on that evaluation.
	describe('first-show measurement', () => {
		beforeEach(() => {
			window.innerWidth = 1000;
			window.innerHeight = 800;
		});

		// Always mounts hidden (mirroring the real store) so the stub is in place before the
		// first positioning; callers then flip `visible` via rerender.
		const renderStubbed = (position: { x: number; y: number }) => {
			const props = makeProps({ visible: false, position });
			const result = render(Tooltip, { props });
			const tooltip = result.container.querySelector('[role="tooltip"]') as HTMLElement;
			stubLayout(tooltip, 280, 180);
			return { props, tooltip, rerender: result.rerender };
		};

		it('flips off the far edges on the very first show once the panel size is measurable', async () => {
			// Well inside the viewport for a 0x0 panel, but a 280x180 panel overflows both edges.
			const { props, tooltip, rerender } = renderStubbed({ x: 850, y: 700 });

			await rerender({ ...props, visible: true });
			flushSync();

			expect(tooltip.style.display).toBe('block');
			// right = 1000 - 850 + 15; bottom = 800 - 700 + 15
			expect(tooltip.style.right).toBe('165px');
			expect(tooltip.style.bottom).toBe('115px');
			expect(tooltip.style.left).toBe('');
			expect(tooltip.style.top).toBe('');
		});

		it('keeps the top-left anchor on first show when the measured panel still fits', async () => {
			const { props, tooltip, rerender } = renderStubbed({ x: 100, y: 100 });

			await rerender({ ...props, visible: true });
			flushSync();

			expect(tooltip.style.left).toBe('115px');
			expect(tooltip.style.top).toBe('115px');
			expect(tooltip.style.right).toBe('');
			expect(tooltip.style.bottom).toBe('');
		});

		it('clears the stale sides when a reposition flips the anchor', async () => {
			const { props, tooltip, rerender } = renderStubbed({ x: 850, y: 700 });

			await rerender({ ...props, visible: true });
			flushSync();
			expect(tooltip.style.right).toBe('165px');
			expect(tooltip.style.bottom).toBe('115px');

			await rerender({ ...props, visible: true, position: { x: 100, y: 100 } });
			flushSync();

			expect(tooltip.style.left).toBe('115px');
			expect(tooltip.style.top).toBe('115px');
			expect(tooltip.style.right).toBe('');
			expect(tooltip.style.bottom).toBe('');
		});

		it('clears the inline display on hide so the stylesheet display:none takes back over', async () => {
			const { props, tooltip, rerender } = renderStubbed({ x: 100, y: 100 });

			await rerender({ ...props, visible: true });
			flushSync();
			expect(tooltip.style.display).toBe('block');

			await rerender({ ...props, visible: false });
			flushSync();

			expect(tooltip.style.display).toBe('');
		});
	});
});
