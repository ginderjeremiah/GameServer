<svg
	width={size}
	height={size}
	viewBox="0 0 14 14"
	fill="none"
	stroke={color}
	stroke-width="1.4"
	stroke-linecap="round"
	stroke-linejoin="round"
	aria-hidden="true"
>
	{#if glyph === 'hit'}
		<path d="M10 1.5l2.5 2.5-7 7-2.5-2.5z M3 11l1.5 1.5" />
	{:else if glyph === 'crit'}
		<path d="M7 1.5l1.6 3.3 3.6.5-2.6 2.6.6 3.6L7 9.7 3.8 11.5l.6-3.6-2.6-2.6 3.6-.5z" />
	{:else if glyph === 'enemy'}
		<path d="M3 11L11 3M11 11L3 3" />
	{:else if glyph === 'loot'}
		<path d="M2.5 4.5h9v8h-9z M5 4.5V3a2 2 0 014 0v1.5" />
	{:else if glyph === 'kill'}
		<path d="M7 2v10M2 7h10" />
	{:else}
		<circle cx="7" cy="7" r="4.5" />
		<path d="M7 5v3M7 9.5v0.01" />
	{/if}
</svg>

<script lang="ts">
import type { GlyphKind } from './log-kind';

interface Props {
	glyph: GlyphKind;
	color: string;
	size?: number;
}

const { glyph, color, size = 11 }: Props = $props();
</script>
