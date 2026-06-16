<div class="drawer-header" style:border-left-color={accent}>
	<TooltipTitle
		label={itemCategoryName(item.itemCategoryId)}
		name={item.name}
		diamondColor={accent}
		labelColor={accent}
	>
		{#snippet trailing()}
			<RarityTag rarityId={item.rarityId} style="margin-left: auto" />
			<button class="close" onclick={() => view.select(null)} aria-label="Close">×</button>
		{/snippet}
	</TooltipTitle>
</div>

<div class="drawer-body">
	{#if attributeMap?.length}
		<TooltipSection label="Stats">
			<TooltipStatsGrid entries={attributeMap} />
		</TooltipSection>
	{/if}

	<TooltipSection label="Mod slots · {item.modSlots?.length ?? 0}">
		<ModSlots {item} {view} />
	</TooltipSection>

	{#if item.description}
		<TooltipSection label="Description" last>
			<TooltipDescription text={item.description} />
		</TooltipSection>
	{/if}
</div>

<div class="drawer-footer">
	<button class="equip-button" class:equipped onclick={() => view.toggleEquip(item)}>
		{equipped ? 'Unequip' : 'Equip'}
		<span class="equip-hint">{equipped ? '⌘·click' : 'or drag'}</span>
	</button>
</div>

<script lang="ts">
import ModSlots from './ModSlots.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import TooltipDescription from '$components/tooltip/TooltipDescription.svelte';
import RarityTag from '$components/RarityTag.svelte';
import { type Item } from '$lib/battle';
import { itemCategoryColor, itemCategoryName } from '$lib/common';
import { type InventoryView } from './inventory-view.svelte';

const { item, view }: { item: Item; view: InventoryView } = $props();

const accent = $derived(itemCategoryColor(item.itemCategoryId));
const equipped = $derived(item.equipmentSlotId != null);

// Merged item + applied-mod attributes, from the item's own source-of-truth
// projection (the same one ItemTooltip reads) so the drawer and tooltip can't
// drift; it recomputes as mods are applied/removed.
const attributeMap = $derived(item.totalAttributes?.getAttributeMap());
</script>

<style lang="scss">
// Padding + the category-row/name chrome now come from the shared TooltipTitle
// primitive; the drawer keeps only its category-coloured left-accent stripe.
.drawer-header {
	border-left: 3px solid;
}

.close {
	border: none;
	background: transparent;
	color: var(--text-tertiary);
	cursor: pointer;
	font-size: 16px;
	line-height: 1;
	padding: 0;
	margin-left: 4px;
}

.drawer-body {
	flex: 1;
	overflow-y: auto;
	padding: 14px 18px;
}

.drawer-footer {
	padding: 16px;
	border-top: 1px solid var(--border-subtle);
}

.equip-button {
	width: 100%;
	padding: 8px 16px;
	font-family: Geist, sans-serif;
	font-size: 12.5px;
	font-weight: 500;
	cursor: pointer;
	background: color-mix(in srgb, var(--accent) 16%, transparent);
	color: var(--accent);
	border: 1px solid color-mix(in srgb, var(--accent) 55%, transparent);
	border-radius: 2px;
	transition: all 120ms;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 8px;

	&:hover {
		background: color-mix(in srgb, var(--accent) 24%, transparent);
	}

	&.equipped {
		background: transparent;
		color: var(--error);
		border-color: color-mix(in srgb, var(--error) 50%, transparent);

		&:hover {
			background: color-mix(in srgb, var(--error) 16%, transparent);
		}
	}
}

.equip-hint {
	font-family: var(--mono);
	font-size: 8.5px;
	opacity: 0.7;
}
</style>
