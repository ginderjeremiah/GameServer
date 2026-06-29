// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import LogGlyph from '$components/log-panel/LogGlyph.svelte';
import type { GlyphKind } from '$components/log-panel/log-kind';

afterEach(cleanup);

const ALL_GLYPHS: GlyphKind[] = [
	'hit',
	'crit',
	'dodge',
	'block',
	'enemy',
	'loot',
	'system',
	'kill',
	'effect',
	'resisted',
	'vulnerable',
	'absorbed'
];

describe('LogGlyph', () => {
	it.each(ALL_GLYPHS)('renders a themed svg path for the "%s" glyph', (glyph) => {
		const { container } = render(LogGlyph, { props: { glyph, color: 'var(--log-player)' } });
		const svg = container.querySelector('svg');
		expect(svg).toBeTruthy();
		expect(svg?.getAttribute('stroke')).toBe('var(--log-player)');
		expect(container.querySelector('svg path')).toBeTruthy();
	});

	it('applies the size prop to the svg (default 11px)', () => {
		const sized = render(LogGlyph, { props: { glyph: 'hit', color: 'red', size: 16 } });
		expect(sized.container.querySelector('svg')?.getAttribute('width')).toBe('16');
		cleanup();
		const dflt = render(LogGlyph, { props: { glyph: 'hit', color: 'red' } });
		expect(dflt.container.querySelector('svg')?.getAttribute('width')).toBe('11');
	});

	it('falls back to a neutral info circle for an unknown glyph', () => {
		const { container } = render(LogGlyph, { props: { glyph: 'unknown' as GlyphKind, color: 'red' } });
		expect(container.querySelector('svg circle')).toBeTruthy();
	});
});
