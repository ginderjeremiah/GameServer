// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import DamageTypeGlyph from '$components/DamageTypeGlyph.svelte';
import type { DamageGlyph } from '$lib/common';

afterEach(cleanup);

const ALL_GLYPHS: DamageGlyph[] = [
	'physical',
	'fire',
	'water',
	'earth',
	'wind',
	'bleed',
	'poison',
	'burn',
	'elemental',
	'dot'
];

describe('DamageTypeGlyph', () => {
	it.each(ALL_GLYPHS)('renders a themed svg path for the "%s" glyph', (glyph) => {
		const { container } = render(DamageTypeGlyph, { props: { glyph, color: 'var(--dmg-fire)' } });
		const svg = container.querySelector('svg');
		expect(svg).toBeTruthy();
		expect(svg?.getAttribute('stroke')).toBe('var(--dmg-fire)');
		// Each known glyph draws at least one outline path.
		expect(container.querySelector('svg path')).toBeTruthy();
	});

	it('applies the size prop to the svg (default 12px)', () => {
		const sized = render(DamageTypeGlyph, { props: { glyph: 'fire', color: 'red', size: 20 } });
		const svg = sized.container.querySelector('svg') as SVGElement;
		expect(svg.getAttribute('width')).toBe('20');
		expect(svg.getAttribute('height')).toBe('20');
		cleanup();
		const dflt = render(DamageTypeGlyph, { props: { glyph: 'fire', color: 'red' } });
		expect(dflt.container.querySelector('svg')?.getAttribute('width')).toBe('12');
	});

	it('falls back to a neutral circle for an unknown glyph', () => {
		const { container } = render(DamageTypeGlyph, {
			props: { glyph: 'unknown' as DamageGlyph, color: 'red' }
		});
		expect(container.querySelector('svg circle')).toBeTruthy();
	});
});
