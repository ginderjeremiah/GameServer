<!-- A compact legend mapping each source swatch to its label, for the sources
     that actually contribute to the selected attribute. -->
<div class="legend">
	{#each sources as source (source)}
		<span class="entry">
			<span class="swatch" style:background={sourceColor(source)}></span>
			<span class="label">{sourceLabel(source)}</span>
		</span>
	{/each}
</div>

<script lang="ts">
import type { EAttributeModifierSource } from '$lib/battle';
import { sourceColor, sourceLabel } from './source-display';

interface Props {
	/** The sources to show, in display order. */
	sources: EAttributeModifierSource[];
}

let { sources }: Props = $props();
</script>

<style lang="scss">
.legend {
	display: flex;
	flex-wrap: wrap;
	gap: 6px 16px;
	align-items: center;
}

.entry {
	display: inline-flex;
	align-items: center;
	gap: 7px;
}

.swatch {
	width: 9px;
	height: 9px;
	border-radius: 1px;
	flex-shrink: 0;
}

.label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}
</style>
