{#if masked}
	<div class="tt-masked-desc">
		{#each lineWidths as width, i (i)}
			<MaskBar {accent} {width} height={7} />
		{/each}
	</div>
{:else}
	<div class="tt-description">{text}</div>
{/if}

<script lang="ts">
import MaskBar from './MaskBar.svelte';

interface Props {
	/** The flavour/description text rendered as the shared italic tooltip body. */
	text?: string;
	/** Render the body as redacted masked lines instead of the real text (sealed/teaser tooltips). */
	masked?: boolean;
	/** Accent hue for the masked lines. Only used when `masked`. */
	accent?: string;
	/** Width (px) of each masked line; one bar is rendered per entry. Only used when `masked`. */
	lineWidths?: number[];
}

const { text = '', masked = false, accent = 'var(--accent)', lineWidths = [236, 170] }: Props = $props();
</script>

<style lang="scss">
.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	line-height: 1.55;
}

.tt-masked-desc {
	display: flex;
	flex-direction: column;
	gap: 4px;
}
</style>
