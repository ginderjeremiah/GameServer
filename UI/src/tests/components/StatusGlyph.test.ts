// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import StatusGlyph from '$components/StatusGlyph.svelte';

afterEach(cleanup);

const svgOf = (container: HTMLElement) => {
	const svg = container.querySelector('svg');
	if (!svg) {
		throw new Error('expected a status glyph svg');
	}
	return svg;
};

describe('StatusGlyph', () => {
	it('is hidden from assistive tech (the status label carries the meaning)', () => {
		const { container } = render(StatusGlyph, { props: { type: 'info' } });
		expect(svgOf(container).getAttribute('aria-hidden')).toBe('true');
	});

	it('reflects the requested size on the svg', () => {
		const { container } = render(StatusGlyph, { props: { type: 'info', size: 20 } });
		const svg = svgOf(container);
		expect(svg.getAttribute('width')).toBe('20');
		expect(svg.getAttribute('height')).toBe('20');
	});

	it('draws a single check stroke for success', () => {
		const { container } = render(StatusGlyph, { props: { type: 'success' } });
		const paths = container.querySelectorAll('path');
		expect(paths).toHaveLength(1);
		expect(paths[0].getAttribute('d')).toBe('M3.5 8.5l3 3 6-6.5');
		expect(container.querySelector('circle')).toBeNull();
	});

	it('draws an X for error', () => {
		const { container } = render(StatusGlyph, { props: { type: 'error' } });
		const paths = container.querySelectorAll('path');
		expect(paths).toHaveLength(1);
		expect(paths[0].getAttribute('d')).toBe('M4 4l8 8M12 4l-8 8');
		expect(container.querySelector('circle')).toBeNull();
	});

	it('draws a triangle with a bang for warning', () => {
		const { container } = render(StatusGlyph, { props: { type: 'warning' } });
		const paths = container.querySelectorAll('path');
		expect(paths).toHaveLength(3);
		expect(paths[0].getAttribute('d')).toBe('M8 2.5l5.5 10.5h-11z');
		expect(container.querySelector('circle')).toBeNull();
	});

	it('draws a circled i for info', () => {
		const { container } = render(StatusGlyph, { props: { type: 'info' } });
		expect(container.querySelector('circle')).not.toBeNull();
		expect(container.querySelectorAll('path')).toHaveLength(2);
	});
});
