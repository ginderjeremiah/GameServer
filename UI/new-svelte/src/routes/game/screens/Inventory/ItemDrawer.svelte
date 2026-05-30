<div class="drawer-header" style:border-left-color={accent}>
	<div class="header-top">
		<span class="cat-diamond" style:background={accent} style:box-shadow="0 0 6px {accent}aa"></span>
		<span class="cat-label" style:color={accent}>{catName(item.itemCategoryId)}</span>
		<span class="rarity-tag">
			<span class="rarity-dot" style:background={rc} style:box-shadow="0 0 6px {hexA(rc, 0.65)}"></span>
			<span class="rarity-label" style:color={rc}>{rarityMeta(item.rarityId).label}</span>
		</span>
		<button class="close" onclick={() => view.select(null)} aria-label="Close">×</button>
	</div>
	<div class="item-name">{item.name}</div>
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
import { BattleAttributes, type Item } from '$lib/battle';
import { catAccent, catName, hexA, rarityColor, rarityMeta, type InventoryView } from './inventory-view.svelte';

const { item, view }: { item: Item; view: InventoryView } = $props();

const accent = $derived(catAccent(item.itemCategoryId));
const rc = $derived(rarityColor(item.rarityId));
const equipped = $derived(item.equipmentSlotId != null);

// Recompute from the item's current attributes + applied mods so the panel
// reflects mod changes live.
const stats = $derived(
	new BattleAttributes([...item.attributes, ...item.appliedMods.flatMap((m) => m.attributes)], false).getAttributeMap()
);
</script>

<style lang="scss">
.drawer-header {
	padding: 16px 18px 14px;
	border-bottom: 1px solid rgba(255, 255, 255, 0.08);
	border-left: 3px solid;
}

.header-top {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 6px;
}

.cat-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	flex-shrink: 0;
}

.cat-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
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
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
}

.close {
	border: none;
	background: transparent;
	color: rgba(240, 240, 240, 0.55);
	cursor: pointer;
	font-size: 16px;
	line-height: 1;
	padding: 0;
	margin-left: 4px;
}

.item-name {
	font-size: 18px;
	font-weight: 400;
	letter-spacing: -0.2px;
	color: #f0f0f0;
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
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);
}

.line {
	flex: 1;
	height: 1px;
	background: rgba(255, 255, 255, 0.08);
}

.description {
	font-size: 11.5px;
	font-style: italic;
	color: rgba(240, 240, 240, 0.55);
	line-height: 1.55;
}

.drawer-footer {
	padding: 16px;
	border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.equip-button {
	width: 100%;
	padding: 8px 16px;
	font-family: Geist, sans-serif;
	font-size: 12.5px;
	font-weight: 500;
	cursor: pointer;
	background: rgba(161, 194, 247, 0.16);
	color: #a1c2f7;
	border: 1px solid rgba(161, 194, 247, 0.55);
	border-radius: 2px;
	transition: all 120ms;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 8px;

	&:hover {
		background: rgba(161, 194, 247, 0.24);
	}

	&.equipped {
		background: transparent;
		color: #f0a094;
		border-color: rgba(240, 160, 148, 0.5);

		&:hover {
			background: rgba(240, 160, 148, 0.16);
		}
	}
}

.equip-hint {
	font-family: 'Geist Mono', monospace;
	font-size: 8.5px;
	opacity: 0.7;
}
</style>
