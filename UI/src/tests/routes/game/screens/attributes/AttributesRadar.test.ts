import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import type { AttributesView } from '$routes/game/screens/attributes/attributes-view.svelte';

import AttributesRadar from '$routes/game/screens/attributes/AttributesRadar.svelte';

const makeView = (overrides: Partial<{ canInc: () => boolean; inc: (i: number) => void }>): AttributesView =>
	({
		values: [5, 5, 5, 5, 5, 5],
		savedValues: [5, 5, 5, 5, 5, 5],
		hexMax: 20,
		canInc: vi.fn(() => true),
		inc: vi.fn(),
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

	it('does not call view.inc when a non-activation key is pressed', async () => {
		const view = makeView({});
		const { container } = render(AttributesRadar, { props: { view } });
		const vertex = container.querySelectorAll('[role="button"]')[0];
		await fireEvent.keyDown(vertex, { key: 'Tab' });
		expect(view.inc).not.toHaveBeenCalled();
	});
});

describe('AttributesRadar — tabindex', () => {
	it('sets tabindex=0 on all vertices when canInc() is true', () => {
		const view = makeView({ canInc: vi.fn(() => true) });
		const { container } = render(AttributesRadar, { props: { view } });
		const vertices = container.querySelectorAll('[role="button"]');
		vertices.forEach((v) => expect(v.getAttribute('tabindex')).toBe('0'));
	});

	it('sets tabindex=-1 on all vertices when canInc() is false', () => {
		const view = makeView({ canInc: vi.fn(() => false) });
		const { container } = render(AttributesRadar, { props: { view } });
		const vertices = container.querySelectorAll('[role="button"]');
		vertices.forEach((v) => expect(v.getAttribute('tabindex')).toBe('-1'));
	});
});
