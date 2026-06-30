<!-- A thin segmented bar encoding a skill's weighted damage-type split (#1343): one segment per
     portion, in authored order, sized proportionally to its weight and tinted by the portion's leaf-type
     hue (the same `--dmg-*` tokens the floater and breakdown read). Surfaces the exact multi-typed mix
     beneath the primary-coloured floater and in the skill tooltips. Purely presentational. -->
<div class="ratio-bar" style:--ratio-bar-height="{height}px" role="presentation">
	{#each portions as portion, i (i)}
		<span class="seg" style:flex-grow={portion.weight} style:background-color={damageTypeColor(portion.type)}></span>
	{/each}
</div>

<script lang="ts">
import type { ISkillDamagePortion } from '$lib/api';
import { damageTypeColor } from '$lib/common';

interface Props {
	/** The weighted leaf-type portions, in authored order; each becomes a segment sized by its weight. */
	portions: readonly ISkillDamagePortion[];
	/** Bar thickness in px. */
	height?: number;
}

const { portions, height = 4 }: Props = $props();
</script>

<style lang="scss">
.ratio-bar {
	display: flex;
	gap: 1px;
	width: 100%;
	height: var(--ratio-bar-height);
	border-radius: 999px;
	overflow: hidden;
}

// Each segment grows in proportion to its portion weight (flex-grow set inline); a zero flex-basis keeps
// the growth purely weight-driven, and min-width: 0 lets a tiny portion still shrink to its share.
.seg {
	flex-basis: 0;
	min-width: 0;
}
</style>
