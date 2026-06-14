/*
 * Single source of truth for tag-category accent visuals (workbench tag UI).
 * The hue is procedural — a deterministic, well-spread value per category id so
 * the set stays scannable — but the lightness, chroma and per-variant alphas are
 * declared as `--tag-*` custom properties in `+layout.svelte` so they remain
 * themeable (mirroring the rarity / challenge-type helpers). These helpers only
 * compose the `oklch()` colour from those variables and the procedural hue.
 */

export interface TagColor {
	fg: string;
	bd: string;
	bg: string;
}

/** Deterministic, well-spread hue per category so the set is scannable. */
const tagHue = (catId: number): number => Math.round((catId * 137.508) % 360);

/**
 * Themeable tag-category accent trio for a category id. The lightness/chroma and
 * the border/background alphas come from the `--tag-*` tokens, so a theme can
 * restyle tag accents while the per-category hue stays procedural.
 */
export const tagColor = (catId: number): TagColor => {
	const hue = tagHue(catId);
	const base = `oklch(var(--tag-lightness) var(--tag-chroma) ${hue}`;
	return {
		fg: `${base})`,
		bd: `${base} / var(--tag-border-alpha))`,
		bg: `${base} / var(--tag-bg-alpha))`
	};
};
