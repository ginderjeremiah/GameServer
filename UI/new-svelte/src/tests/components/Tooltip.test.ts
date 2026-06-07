import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import Tooltip from '$components/Tooltip.svelte';

afterEach(cleanup);

interface TooltipProps {
	component: () => { getBaseNode: () => HTMLDivElement };
	visible: boolean;
	position?: { x: number; y: number };
	id: number;
}

const makeProps = (overrides: Partial<TooltipProps> = {}): TooltipProps => ({
	id: 1,
	component: () => undefined!,
	visible: false,
	position: undefined,
	...overrides
});

describe('Tooltip', () => {
	it('renders a container with role="tooltip"', () => {
		const { container } = render(Tooltip, { props: makeProps() });
		expect(container.querySelector('[role="tooltip"]')).toBeTruthy();
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
});
