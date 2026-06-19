<!-- A horizontal stacked bar decomposing a computed attribute into its additive
     source segments (and a striped cap for any multiplicative uplift). By
     default it fills its full width and shows the source *proportions* of this
     attribute alone, so a high-magnitude stat (e.g. Max Health) doesn't flatten
     the others when these bars sit in a list. -->
<div class="stack" style:height="{height}px" style:border-radius="{radius}px" role="presentation">
	{#each groups as group (group.source)}
		{@const width = Math.max(0, (group.total / scale) * 100)}
		{#if width > 0}
			<div
				class="seg"
				style:width="{width}%"
				style:background={sourceColor(group.source)}
				title="{sourceLabel(group.source)}: {fmtSigned(group.total, 1)}"
			></div>
		{/if}
	{/each}
	{#each mults as mult, i (i)}
		{@const width = Math.max(0, (mult.applied / scale) * 100)}
		{#if width > 0}
			<div
				class="seg mult"
				style:width="{width}%"
				style:--mult-color={sourceColor(mult.source)}
				title="×{mult.factor} → {fmtSigned(mult.applied, 1)}"
			></div>
		{/if}
	{/each}
</div>

<script lang="ts">
import { groupBySource, type ComputedAttribute, type GroupedBySource } from '$lib/battle';
import { sourceColor, sourceLabel } from './source-display';
import { fmtSigned, type LabeledModifier } from './attribute-breakdown-view.svelte';

interface Props {
	computed: ComputedAttribute<LabeledModifier>;
	/** Pre-grouped source decomposition; derived from `computed` when omitted. Pass
	 *  the view-model's already-computed grouping to avoid re-running groupBySource. */
	grouped?: GroupedBySource<LabeledModifier>;
	height?: number;
	radius?: number;
	/** Width the full bar represents; defaults to this attribute's own total so
	 *  the bar fills and shows source proportions. */
	scaleMax?: number;
}

let { computed, grouped, height = 14, radius = 2, scaleMax }: Props = $props();

const resolved = $derived(grouped ?? groupBySource(computed));
const groups = $derived(resolved.groups);
const mults = $derived(resolved.mults);
const scale = $derived(scaleMax || Math.max(computed.total, computed.additiveSubtotal) || 1);
</script>

<style lang="scss">
.stack {
	display: flex;
	width: 100%;
	overflow: hidden;
	background: color-mix(in srgb, var(--white) 5%, transparent);
	box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--white) 6%, transparent);
}

.seg {
	height: 100%;
	opacity: 0.92;
	border-right: 1px solid color-mix(in srgb, var(--page) 55%, transparent);

	&:last-child {
		border-right: none;
	}
}

.mult {
	background: repeating-linear-gradient(
		135deg,
		color-mix(in srgb, var(--mult-color) 80%, transparent) 0 3px,
		color-mix(in srgb, var(--mult-color) 33%, transparent) 3px 6px
	);
}
</style>
