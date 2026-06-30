<!-- The portion mix of a skill's direct hit (#1343): the segmented ratio bar (multi-typed only) plus
     one labelled row per leaf type with its share of the hit. A single-typed skill shows just its type
     — no percentage, no bar — so the common case stays clean. Shared by the combat skill tooltip and
     the Skills-screen inspector so both read the same way. Colour / icon / name are a pure frontend
     concern, resolved through the `damage-type-display` helpers. -->
<div class="dmg-types">
	{#if multi}
		<DamageRatioBar {portions} />
	{/if}
	<div class="dt-list" class:multi>
		{#each portions as portion, i (i)}
			<div class="dt-row">
				<img class="dt-ico" src={damageTypeIcon(portion.type)} alt="" />
				<span class="dt-name" style:color={damageTypeColor(portion.type)}>{damageTypeName(portion.type)}</span>
				{#if multi}
					<span class="dt-pct">{percent(portion.weight)}%</span>
				{/if}
			</div>
		{/each}
	</div>
</div>

<script lang="ts">
import type { ISkillDamagePortion } from '$lib/api';
import { damageTypeColor, damageTypeIcon, damageTypeName } from '$lib/common';
import DamageRatioBar from './DamageRatioBar.svelte';

interface Props {
	/** The skill's weighted leaf-type portions, in authored order. */
	portions: readonly ISkillDamagePortion[];
}

const { portions }: Props = $props();

const multi = $derived(portions.length > 1);
const totalWeight = $derived(portions.reduce((sum, portion) => sum + portion.weight, 0));

/** A portion's share of the hit as a rounded percentage; an even split is the fallback when weights sum
 *  to a non-positive total (a malformed authoring state the Workbench editor's validation prevents). */
const percent = (weight: number): number =>
	totalWeight > 0 ? Math.round((weight / totalWeight) * 100) : Math.round(100 / portions.length);
</script>

<style lang="scss">
.dmg-types {
	display: flex;
	flex-direction: column;
	gap: 7px;
}

.dt-list {
	display: flex;
	flex-direction: column;
	gap: 4px;

	// A multi-typed mix labels each share, so give the rows a touch more breathing room.
	&.multi {
		gap: 5px;
	}
}

.dt-row {
	display: flex;
	align-items: center;
	gap: 6px;
	font-size: 12px;
}

.dt-ico {
	width: 14px;
	height: 14px;
	object-fit: contain;
	flex-shrink: 0;
}

.dt-name {
	color: var(--text-secondary);
}

.dt-pct {
	margin-left: auto;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);
	letter-spacing: 0.3px;
}
</style>
