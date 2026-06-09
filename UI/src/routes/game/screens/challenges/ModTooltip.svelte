<TooltipShell accent={rarityColor(mod.rarityId)}>
	{#snippet header()}
		<TooltipTitle
			label={modTypeLabel(mod.itemModTypeId)}
			name={mod.name}
			diamondColor={typeColor}
			labelColor={typeColor}
		/>
	{/snippet}

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
</TooltipShell>

<script lang="ts">
import { EAttribute, type IItemMod } from '$lib/api';
import { modTypeColor, modTypeLabel, normalizeText, rarityColor } from '$lib/common';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
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
.tt-description {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	line-height: 1.55;
}
</style>
