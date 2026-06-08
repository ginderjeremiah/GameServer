<div class="tt-masked-grid">
	{#each indices as i (i)}
		<MaskBar {accent} width={barWidths[i % barWidths.length]} />
		<div class="tt-qmark-cell"><span class="tt-qmark" style:color={tintColor(accent, 0.7)}>???</span></div>
	{/each}
</div>

<script lang="ts">
import { tintColor } from '$lib/common';
import MaskBar from './MaskBar.svelte';

interface Props {
	/** Number of masked rows — one per real stat/effect, so the *count* still reads true. */
	rows: number;
	/** Bar widths cycled across the rows so the teaser doesn't look uniform. */
	barWidths: number[];
	/** Accent hue for the bars and the `???` placeholders. */
	accent: string;
}

const { rows, barWidths, accent }: Props = $props();

const indices = $derived([...Array(rows).keys()]);
</script>

<style lang="scss">
.tt-masked-grid {
	display: grid;
	grid-template-columns: 1fr auto;
	row-gap: 6px;
	column-gap: 12px;
	align-items: center;
}

.tt-qmark-cell {
	text-align: right;
}

.tt-qmark {
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 1px;
}
</style>
