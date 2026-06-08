<div class="drawer-header" style:border-left-color={accent}>
	<TooltipTitle
		label={itemCategoryName(item.itemCategoryId)}
		name={item.name}
		diamondColor={accent}
		labelColor={accent}
	>
		{#snippet trailing()}
			<span class="rarity-tag">
				<span class="rarity-dot" style:background={rc} style:box-shadow="0 0 6px {rarityTint(item.rarityId, 0.65)}"
				></span>
				<span class="rarity-label" style:color={rc}>{rarityLabel(item.rarityId)}</span>
			</span>
			<button class="close" onclick={() => view.select(null)} aria-label="Close">×</button>
		{/snippet}
	</TooltipTitle>
</div>

<div class="drawer-body">
	<div class="section">
		<div class="section-rule">
			<span class="mono-label">Stats</span>
			<div class="line"></div>
		</div>
		<StatList attrs={stats} />
	</div>

	<div class="section">
		<div class="section-rule">
			<span class="mono-label">Mod slots · {item.modSlots?.length ?? 0}</span>
			<div class="line"></div>
		</div>
		<ModSlots {item} {view} />
	</div>

	{#if item.description}
		<div class="description">{item.description}</div>
	{/if}
</div>

<div class="drawer-footer">
	<button class="equip-button" class:equipped onclick={() => view.toggleEquip(item)}>
		{equipped ? 'Unequip' : 'Equip'}
		<span class="equip-hint">{equipped ? '⌘·click' : 'or drag'}</span>
	</button>
</div>

<script lang="ts">
import StatList from './StatList.svelte';
import ModSlots from './ModSlots.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import { BattleAttributes, type Item } from '$lib/battle';
import { itemCategoryColor, itemCategoryName, rarityColor, rarityLabel, rarityTint } from '$lib/common';
import { type InventoryView } from './inventory-view.svelte';

const { item, view }: { item: Item; view: InventoryView } = $props();

const accent = $derived(itemCategoryColor(item.itemCategoryId));
const rc = $derived(rarityColor(item.rarityId));
const equipped = $derived(item.equipmentSlotId != null);

// Recompute from the item's current attributes + applied mods so the panel
// reflects mod changes live.
const stats = $derived(
	new BattleAttributes([...item.attributes, ...item.appliedMods.flatMap((m) => m.attributes)], false).getAttributeMap()
);
</script>

<style lang="scss">
// Padding + the category-row/name chrome now come from the shared TooltipTitle
// primitive; the drawer keeps only its category-coloured left-accent stripe.
.drawer-header {
	border-left: 3px solid;
}

.rarity-tag {
	margin-left: auto;
	display: flex;
	align-items: center;
	gap: 6px;
}

.rarity-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
}

.rarity-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
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

.section {
	margin-bottom: 16px;
}

.section-rule {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 10px;
}

.mono-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.line {
	flex: 1;
	height: 1px;
	background: var(--border-subtle);
}

.description {
	font-size: 11.5px;
	font-style: italic;
	color: var(--text-tertiary);
	line-height: 1.55;
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
