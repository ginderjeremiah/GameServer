<!-- A small inline-SVG glyph for a damage type (#1320, Area F). Outline style, themed via the passed
     `color` (a `var(--dmg-*)` hue from `damageTypeKeyColor`). Decorative — every surface that shows it
     also renders the type's name or a damage number alongside, so it carries no standalone label. -->
<svg
	width={size}
	height={size}
	viewBox="0 0 14 14"
	fill="none"
	stroke={color}
	stroke-width="1.3"
	stroke-linecap="round"
	stroke-linejoin="round"
	aria-hidden="true"
>
	{#if glyph === 'physical'}
		<!-- An impact burst. -->
		<path d="M7 1.5l1 4.5 4.5 1-4.5 1-1 4.5-1-4.5-4.5-1 4.5-1z" />
	{:else if glyph === 'fire'}
		<!-- A flame with an inner curl. -->
		<path d="M6.6 1.6C8.8 4.3 10 6 10 8.1A3 3 0 1 1 4 8.1c0-1.3.7-1.9 1.2-1.5 0-1.8.6-3.2 1.4-5z" />
	{:else if glyph === 'water'}
		<!-- A droplet. -->
		<path d="M7 2c2.6 3.4 3 4.7 3 6.3a3 3 0 1 1-6 0C4 6.7 4.4 5.4 7 2z" />
	{:else if glyph === 'earth'}
		<!-- A mountain range. -->
		<path d="M1.8 11l3.2-6 2.2 3.5 1.8-2.5 3.2 5z" />
	{:else if glyph === 'wind'}
		<!-- Gusting lines that curl back. -->
		<path d="M2.5 5h5.5a1.5 1.5 0 1 0-1.5-1.5M2.5 8.5h7a1.5 1.5 0 1 1-1.5 1.5" />
	{:else if glyph === 'bleed'}
		<!-- Three claw slashes. -->
		<path d="M4 2.5c1 2.5 1.5 5.5 1.2 8.5M7 2.5c1 2.5 1.5 5.5 1.2 8.5M10 3c.6 2.2.8 4.5.6 6.7" />
	{:else if glyph === 'poison'}
		<!-- A toxic droplet with bubbles. -->
		<path d="M7 3c2 2.6 2.4 3.7 2.4 5.1a2.4 2.4 0 1 1-4.8 0C4.6 6.7 5 5.6 7 3z" />
		<path d="M3 4.3h.01M11 5.3h.01" />
	{:else if glyph === 'burn'}
		<!-- A flame with a secondary lick. -->
		<path d="M6.4 2C8.4 4.5 9.4 6 9.4 8A2.7 2.7 0 1 1 4 8c0-1.6 1-2.4 2.4-6z" />
		<path d="M9.2 2.6c1.1 1.3 1.6 2.3 1.4 3.5" />
	{:else if glyph === 'elemental'}
		<!-- A crystalline gem. -->
		<path d="M7 1.5L11 7l-4 5.5L3 7z M7 4.5v5M4.5 7h5" />
	{:else if glyph === 'dot'}
		<!-- A clock (damage over time). -->
		<path d="M2.5 7a4.5 4.5 0 1 0 9 0 4.5 4.5 0 1 0-9 0M7 7V4.2M7 7l2 1.3" />
	{:else}
		<circle cx="7" cy="7" r="4.5" />
	{/if}
</svg>

<script lang="ts">
import type { DamageGlyph } from '$lib/common';

interface Props {
	glyph: DamageGlyph;
	color: string;
	size?: number;
}

const { glyph, color, size = 12 }: Props = $props();
</script>
