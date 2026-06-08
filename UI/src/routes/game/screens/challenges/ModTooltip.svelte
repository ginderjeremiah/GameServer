<div class="mod-tooltip" style:border-left="3px solid {rarityColor(mod.rarityId)}">
	<TooltipTitle
		label={modTypeLabel(mod.itemModTypeId)}
		name={mod.name}
		diamondColor={typeColor}
		labelColor={typeColor}
	/>

	<div class="tt-body">
		{#if effects.length}
			<TooltipSection label="Effects" last={!mod.description}>
				<TooltipStatsGrid entries={effects} />
			</TooltipSection>
		{/if}

		{#if mod.description}
			<TooltipSection label="Description" last>
				<div class="tt-description">{mod.description}</div>
			</TooltipSection>
		{/if}
	</div>
</div>

<script lang="ts">
import { EAttribute, type IItemMod } from '$lib/api';
import { modTypeColor, modTypeLabel, normalizeText, rarityColor } from '$lib/common';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';

interface Props {
	mod: IItemMod;
}

const { mod }: Props = $props();

const typeColor = $derived(modTypeColor(mod.itemModTypeId));
const effects = $derived(
	(mod.attributes ?? []).map((a) => ({ name: normalizeText(EAttribute[a.attributeId]), value: a.amount }))
);
</script>

<style lang="scss">
.mod-tooltip {
	width: 280px;
	border-radius: 3px;
	box-shadow: -4px 0 16px color-mix(in srgb, var(--black) 15%, transparent);
}

.tt-body {
	padding: 12px 16px 14px;
}

.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	line-height: 1.55;
}
</style>
