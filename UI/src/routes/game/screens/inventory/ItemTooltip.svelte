<TooltipShell accent={rarityAccent} hidden={!item} bind:base={container}>
	{#snippet header()}
		<TooltipTitle label={categoryName} name={displayName} diamondColor={categoryColor} labelColor={categoryColor}>
			{#snippet trailing()}
				{#if item?.equipped}
					<EquippedBadge />
				{/if}
			{/snippet}
		</TooltipTitle>
	{/snippet}

	<!-- Stats -->
	{#if attributeMap?.length}
		<TooltipSection label="Stats">
			<TooltipStatsGrid entries={attributeMap} />
		</TooltipSection>
	{/if}

	<!-- Mods — every slot, filled or empty -->
	{#if modSlots.length}
		<TooltipSection label="Mods · {filledCount}/{modSlots.length}">
			<ModList slots={modSlots} />
		</TooltipSection>
	{/if}

	<!-- Description -->
	{#if item?.description}
		<TooltipSection label="Description" last>
			<TooltipDescription text={item.description} />
		</TooltipSection>
	{/if}
</TooltipShell>

<script lang="ts">
import type { Item } from '$lib/battle';
import { composeItemName, itemCategoryColor, itemCategoryName, rarityColor } from '$lib/common';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import TooltipDescription from '$components/tooltip/TooltipDescription.svelte';
import EquippedBadge from './item-tooltip/EquippedBadge.svelte';
import ModList from './item-tooltip/ModList.svelte';

export const getBaseNode = () => container;

type Props = {
	item: Item | undefined;
};

const { item }: Props = $props();

// Bound to the shell's root element and relocated into the global tooltip container
// by getBaseNode(); reactive so the relocation runs once the shell has mounted.
let container = $state<HTMLDivElement>();

const attributeMap = $derived(item?.totalAttributes?.getAttributeMap());

// Every mod slot (filled or empty), so the tooltip surfaces open slots too.
const modSlots = $derived(
	(item?.modSlots ?? []).map((slot) => ({
		slotId: slot.id,
		type: slot.itemModSlotTypeId,
		mod: item?.appliedMods.find((m) => m.itemModSlotId === slot.id) ?? null
	}))
);
const filledCount = $derived(modSlots.filter((s) => s.mod).length);

// The tooltip's main accent (left border) reflects the item's rarity, while the
// category row (diamond + label) stays category-coloured — mirroring how
// ModTooltip accents its border by rarity and its diamond/label by mod type.
const rarityAccent = $derived(item ? rarityColor(item.rarityId) : 'var(--rarity-common)');
const categoryColor = $derived(item ? itemCategoryColor(item.itemCategoryId) : 'var(--category-armor)');
const categoryName = $derived(item ? itemCategoryName(item.itemCategoryId) : 'Item');
// Item name reflects its applied mods: prefix mod names prepend, suffix names append.
const displayName = $derived(item ? composeItemName(item.name, item.appliedMods) : '');
</script>
