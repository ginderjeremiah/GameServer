<div
	class="item-tooltip"
	bind:this={container}
	style={item ? '' : 'display: none;'}
	style:border-left="3px solid {rarityAccent}"
>
	{#if item}
		<TooltipTitle label={categoryName} name={displayName} diamondColor={categoryColor} labelColor={categoryColor}>
			{#snippet trailing()}
				{#if item.equipped}
					<EquippedBadge />
				{/if}
			{/snippet}
		</TooltipTitle>

		<div class="tt-body">
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
			{#if item.description}
				<TooltipSection label="Description" last>
					<div class="tt-description">{item.description}</div>
				</TooltipSection>
			{/if}
		</div>
	{/if}
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import { composeItemName, itemCategoryColor, itemCategoryName, rarityColor } from '$lib/common';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import EquippedBadge from './item-tooltip/EquippedBadge.svelte';
import ModList from './item-tooltip/ModList.svelte';

export const getBaseNode = () => container;

type Props = {
	item: Item | undefined;
};

const { item }: Props = $props();

let container: HTMLDivElement;

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

<style lang="scss">
.item-tooltip {
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
