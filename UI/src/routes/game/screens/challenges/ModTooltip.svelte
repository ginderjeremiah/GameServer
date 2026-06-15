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
			<TooltipDescription text={mod.description} />
		</TooltipSection>
	{/if}
</TooltipShell>

<script lang="ts">
import { type IItemMod } from '$lib/api';
import { attributeName, modTypeColor, modTypeLabel, rarityColor } from '$lib/common';
import { staticData } from '$stores';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import TooltipDescription from '$components/tooltip/TooltipDescription.svelte';

interface Props {
	mod: IItemMod;
}

const { mod }: Props = $props();

const typeColor = $derived(modTypeColor(mod.itemModTypeId));
const effects = $derived(
	(mod.attributes ?? []).map((a) => ({ name: attributeName(a.attributeId, staticData.attributes), value: a.amount }))
);
</script>
