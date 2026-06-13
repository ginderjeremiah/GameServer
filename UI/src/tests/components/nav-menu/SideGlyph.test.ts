import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import SideGlyph from '../../../components/nav-menu/SideGlyph.svelte';
import { GAME_SCREENS } from '$routes/game/screens/screen-defs';

afterEach(cleanup);

const SHAPE_SELECTOR = 'path, rect, circle, line, polygon, polyline';

describe('SideGlyph', () => {
	// Guards against shipping a sidebar screen whose key has no matching glyph branch (the Skills
	// tab regression, #434): an unmatched kind renders an empty <svg> with no drawn shapes.
	it('renders a drawn glyph for every game screen key', () => {
		for (const screen of GAME_SCREENS) {
			const { container } = render(SideGlyph, { props: { kind: screen.key } });
			const shapes = container.querySelectorAll(SHAPE_SELECTOR);

			expect(shapes.length, `expected a drawn glyph for screen "${screen.key}"`).toBeGreaterThan(0);
			cleanup();
		}
	});

	it('strokes with the accent colour when active', () => {
		const { container } = render(SideGlyph, { props: { kind: 'skills', active: true } });
		const svg = container.querySelector('svg');

		expect(svg?.getAttribute('stroke')).toContain('var(--accent)');
	});
});
