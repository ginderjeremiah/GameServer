<svg
	width={size}
	height={size}
	viewBox="0 0 16 16"
	fill="none"
	stroke={color}
	stroke-width="1.4"
	stroke-linecap="round"
	stroke-linejoin="round"
	aria-hidden="true"
>
	{#if glyph === 'logging'}
		<path d="M3 3.5h10M3 6.5h10M3 9.5h7M3 12.5h5" />
	{:else if glyph === 'display'}
		<rect x="2" y="3" width="12" height="8.5" rx="1" />
		<path d="M6 14h4M8 11.5V14" />
	{:else if glyph === 'audio'}
		<path d="M3 6v4h2.5L9 13V3L5.5 6z" />
		<path d="M11.5 5.5a3.5 3.5 0 010 5" />
	{:else if glyph === 'gameplay'}
		<rect x="2.5" y="5" width="11" height="6.5" rx="3.25" />
		<path d="M5 8h2M6 7v2" />
		<circle cx="10.7" cy="8" r="0.6" fill={color} stroke="none" />
	{:else if glyph === 'account'}
		<circle cx="8" cy="5.5" r="2.5" />
		<path d="M3.5 13c0-2.5 2-4 4.5-4s4.5 1.5 4.5 4" />
	{/if}
</svg>

<script lang="ts">
import type { SettingsGlyphKind } from './options-view.svelte';

interface Props {
	glyph: SettingsGlyphKind;
	color?: string;
	size?: number;
}

const { glyph, color = 'currentColor', size = 15 }: Props = $props();
</script>
