import { describe, it, expect } from 'vitest';
import { tagColor } from '$lib/common';

describe('tagColor', () => {
	it('composes the accent trio from the themeable --tag-* tokens and a procedural hue', () => {
		const hue = Math.round((100 * 137.508) % 360);
		expect(tagColor(100)).toEqual({
			fg: `oklch(var(--tag-lightness) var(--tag-chroma) ${hue})`,
			bd: `oklch(var(--tag-lightness) var(--tag-chroma) ${hue} / var(--tag-border-alpha))`,
			bg: `oklch(var(--tag-lightness) var(--tag-chroma) ${hue} / var(--tag-bg-alpha))`
		});
	});

	it('is deterministic for a given category id', () => {
		expect(tagColor(100)).toEqual(tagColor(100));
	});

	it('spreads distinct hues across categories', () => {
		expect(tagColor(200).fg).not.toBe(tagColor(100).fg);
	});
});
