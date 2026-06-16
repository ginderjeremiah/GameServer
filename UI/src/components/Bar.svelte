<!-- Generic progress/fill bar primitive: a rounded track with a percentage-width fill plus the
     ARIA `progressbar` wiring. The visual treatment (height, radius, track/fill colour, glow and
     transition) is supplied by the consumer through `--bar-*` CSS custom properties so every accent
     stays a themeable token; an optional `children` snippet overlays extra marks (e.g. a position
     cursor) inside the track. Text-bearing/multi-layer bars (`HpBar`) stay specialised. -->
<div
	class="bar-track"
	role="progressbar"
	aria-label={ariaLabel}
	aria-valuenow={Math.round(clampedValue)}
	aria-valuemin={min}
	aria-valuemax={max}
	aria-valuetext={valueText}
	data-testid={testId}
>
	<div class="bar-fill" style:width="{fillPercent}%"></div>
	{@render children?.()}
</div>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Current value, interpreted against `min`/`max`. */
	value: number;
	/** Upper bound of the value range (defaults to a 0–100 percentage scale). */
	max?: number;
	/** Lower bound of the value range. */
	min?: number;
	/** Accessible name for the bar; omitted from the DOM when not provided. */
	ariaLabel?: string;
	/** Human-readable value (sets `aria-valuetext`); omitted when not provided. */
	valueText?: string;
	testId?: string;
	/** Optional overlay rendered inside the track, above the fill (e.g. a position cursor). */
	children?: Snippet;
}

const { value, max = 100, min = 0, ariaLabel, valueText, testId, children }: Props = $props();

const clampedValue = $derived(Math.min(max, Math.max(min, value)));
// Guard the degenerate zero-width range so the fill stays empty rather than NaN.
const fillPercent = $derived(max > min ? ((clampedValue - min) / (max - min)) * 100 : 0);
</script>

<style lang="scss">
.bar-track {
	position: relative;
	width: 100%;
	height: var(--bar-height, 8px);
	border-radius: var(--bar-radius, 30px);
	background: var(--bar-track-bg, color-mix(in srgb, var(--white) 6%, transparent));
	box-shadow: var(--bar-track-shadow, none);
	overflow: hidden;
}

.bar-fill {
	position: absolute;
	inset: 0;
	border-radius: inherit;
	background: var(--bar-fill, var(--accent));
	box-shadow: var(--bar-fill-shadow, none);
	transition: var(--bar-transition, width 0.14s linear);
}
</style>
