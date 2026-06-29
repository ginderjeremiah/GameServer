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
	{:else if glyph === 'dodge'}
		<!-- Two motion chevrons suggesting an evasive slip aside. -->
		<path d="M2.5 3.5l3 3.5-3 3.5M7 3.5l3 3.5-3 3.5" />
	{:else if glyph === 'enemy'}
		<path d="M3 11L11 3M11 11L3 3" />
	{:else if glyph === 'loot'}
		<path d="M2.5 4.5h9v8h-9z M5 4.5V3a2 2 0 014 0v1.5" />
	{:else if glyph === 'kill'}
		<path d="M7 2v10M2 7h10" />
	{:else if glyph === 'effect'}
		<path d="M7 1.5l4.5 2.6v5.8L7 12.5 2.5 9.9V4.1z M7 5v2.5M7 9v.01" />
	{:else if glyph === 'resisted'}
		<!-- A downward arrow: the hit was reduced. -->
		<path d="M7 2v8M3.5 6.5L7 10l3.5-3.5" />
	{:else if glyph === 'vulnerable'}
		<!-- An upward arrow: the hit was amplified. -->
		<path d="M7 12V4M3.5 7.5L7 4l3.5 3.5" />
	{:else if glyph === 'absorbed'}
		<!-- A shield with a plus: the hit was absorbed into a heal. -->
		<path d="M7 1.8l4.3 1.7v3.4c0 3-1.9 4.6-4.3 5.8C4.6 11.5 2.7 9.9 2.7 6.9V3.5z M7 5v3M5.5 6.5h3" />
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
