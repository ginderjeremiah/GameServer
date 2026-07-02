<!-- A simple geometric glyph standing in for each breakdown kind: a diamond "skull" for enemies,
     a hex tile for zones, a rune mark for skills, a spark for the damage-type breakdown. -->
<svg {...dims} viewBox="0 0 16 16" fill="none" {stroke} stroke-width="1.3" aria-hidden="true">
	{#if kind === 'enemy'}
		<path d="M8 1.5l6 6.5-6 6.5-6-6.5z" stroke-linejoin="round" />
		<circle cx="6" cy="7.5" r="1" fill={stroke} stroke="none" />
		<circle cx="10" cy="7.5" r="1" fill={stroke} stroke="none" />
	{:else if kind === 'zone'}
		<path d="M8 1.5l5.5 3.2v6.6L8 14.5 2.5 11.3V4.7z" stroke-linejoin="round" />
	{:else if kind === 'damageType'}
		<path d="M8 1.5l1.8 4.7 4.7 1.8-4.7 1.8L8 14.5l-1.8-4.7-4.7-1.8 4.7-1.8z" stroke-linejoin="round" />
	{:else}
		<path d="M8 2v12M4 5l8 6M12 5l-8 6" stroke-linecap="round" />
	{/if}
</svg>

<script lang="ts">
import type { StatBreakdownKind } from './statistics-view.svelte';
import { statKindColor } from './statistics-display';

interface Props {
	kind: StatBreakdownKind;
	size?: number;
	/** Override colour; defaults to the kind accent. */
	color?: string;
}

let { kind, size = 16, color }: Props = $props();

const dims = $derived({ width: size, height: size, style: 'display:block' });
const stroke = $derived(color ?? statKindColor(kind));
</script>
