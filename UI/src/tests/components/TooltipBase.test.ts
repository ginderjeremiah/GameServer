import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { flushSync } from 'svelte';

// Use vi.hoisted so the array reference is available inside the vi.mock factory.
const { mockTooltipsData } = vi.hoisted(() => ({
	mockTooltipsData: [] as { id: number; component: () => undefined; visible: boolean }[]
}));

// Mocking $stores lets us control the tooltip data without touching the real tooltip store
// (which uses SvelteMap and is a global singleton).
vi.mock('$stores', () => ({
	tooltips: {
		get data() {
			return mockTooltipsData;
		}
	},
	// Tooltip.svelte derives its container id from this.
	tooltipElementId: (id: number) => `tooltip-${id}`
}));

import TooltipBase from '$components/TooltipBase.svelte';

afterEach(() => {
	cleanup();
	mockTooltipsData.length = 0;
});

describe('TooltipBase', () => {
	it('renders no tooltip containers when there are no tooltips', () => {
		const { container } = render(TooltipBase);
		expect(container.querySelectorAll('[role="tooltip"]').length).toBe(0);
	});

	it('renders one tooltip container per tooltip in the store', () => {
		mockTooltipsData.push({ id: 1, component: () => undefined, visible: false });
		mockTooltipsData.push({ id: 2, component: () => undefined, visible: false });

		const { container } = render(TooltipBase);
		flushSync();

		expect(container.querySelectorAll('[role="tooltip"]').length).toBe(2);
	});
});
