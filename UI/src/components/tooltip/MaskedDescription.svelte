<div class="tt-masked-desc">
	{#each lineWidths as width, i (i)}
		<MaskBar {accent} {width} height={7} />
	{/each}
</div>

<script lang="ts">
import MaskBar from './MaskBar.svelte';

interface Props {
	/** Accent hue for the masked lines. */
	accent: string;
	/** Width (px) of each masked line; one bar is rendered per entry. */
	lineWidths?: number[];
}

const { accent, lineWidths = [236, 170] }: Props = $props();
</script>

<style lang="scss">
.tt-masked-desc {
	display: flex;
	flex-direction: column;
	gap: 4px;
}
</style>
