<TooltipShell accent={rarityAccent}>
	{#snippet header()}
		<TooltipTitle
			label={modTypeLabel(mod.itemModTypeId)}
			name={mod.name}
			diamondColor={typeColor}
			labelColor={typeColor}
			{masked}
			sealedAccent={rarityAccent}
		/>
	{/snippet}

	{#if effects.length}
		<TooltipSection label="Effects" last={!showDescription}>
			<TooltipStatsGrid entries={effects} {masked} accent={rarityAccent} barWidths={EFFECT_BAR_WIDTHS} />
		</TooltipSection>
	{/if}

	{#if showDescription}
		<TooltipSection label="Description" last>
			<TooltipDescription text={mod.description} {masked} accent={rarityAccent} lineWidths={DESC_LINE_WIDTHS} />
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
	/** Render a sealed teaser (masked name, redacted effects/description) instead of the real mod. */
	masked?: boolean;
}

const { mod, masked = false }: Props = $props();

const rarityAccent = $derived(rarityColor(mod.rarityId));
const typeColor = $derived(modTypeColor(mod.itemModTypeId));
// One row per attribute; the redacted teaser only needs the count, so values stay safely unused.
const effects = $derived(
	(mod.attributes ?? []).map((a) => ({ name: attributeName(a.attributeId, staticData.attributes), value: a.amount }))
);
// The sealed teaser always shows a (masked) description; the real tooltip only when there is one.
const showDescription = $derived(masked || !!mod.description);

const EFFECT_BAR_WIDTHS = [70, 88, 58];
const DESC_LINE_WIDTHS = [236, 170];
</script>
