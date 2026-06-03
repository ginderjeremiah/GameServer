<div class="equip-row">
	<div class="row-label">{slot.label}</div>

	<div
		class="equip-tile"
		class:filled
		class:over
		class:selected
		class:can-accept={canAccept}
		role="button"
		tabindex="0"
		style:border-color={tileBorder}
		style:background={over
			? hexA('#a1c2f7', 0.14)
			: filled
				? rarityTint(item!.rarityId, 0.07)
				: 'rgba(255,255,255,0.03)'}
		ondragover={handleDragOver}
		ondragleave={() => (over = false)}
		ondrop={handleDrop}
		onclick={() => filled && onSelect?.(item!)}
		onkeydown={(e) => {
			if (filled && (e.key === 'Enter' || e.key === ' ')) {
				e.preventDefault();
				onSelect?.(item!);
			}
		}}
		onmouseenter={(e) => {
			hover = true;
			if (filled) onHoverEnter?.(item!, e);
		}}
		onmousemove={(e) => filled && onHoverMove?.(e)}
		onmouseleave={() => {
			hover = false;
			onHoverLeave?.();
		}}
	>
		{#if filled}
			<CategoryGlyph cat={item!.itemCategoryId} color={catAccent(item!.itemCategoryId)} size={20} />
			{#if hover}
				<button
					class="unequip"
					title="Unequip"
					onclick={(e) => {
						e.stopPropagation();
						onUnequip?.(slot.id);
					}}>×</button
				>
			{/if}
			{#if item!.appliedMods.length}
				<span class="mod-count">{item!.appliedMods.length}◈</span>
			{/if}
		{:else}
			<CategoryGlyph cat={slot.category} color="rgba(240,240,240,0.18)" size={19} />
		{/if}
	</div>

	<div class="row-info">
		{#if filled}
			<div class="item-name">{item!.name}</div>
			<div class="rarity-tag">
				<span class="rarity-dot" style:background={rc} style:box-shadow="0 0 6px {rarityTint(item!.rarityId, 0.65)}"
				></span>
				<span class="rarity-label" style:color={rc}>{rarityLabel(item!.rarityId)}</span>
			</div>
		{:else}
			<div class="empty-label">Empty</div>
		{/if}
	</div>
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import { rarityColor, rarityLabel, rarityTint } from '$lib/common';
import CategoryGlyph from './CategoryGlyph.svelte';
import { catAccent, hexA, type EquipSlotDef } from './inventory-view.svelte';

interface Props {
	slot: EquipSlotDef;
	item?: Item;
	dragItem?: Item | null;
	selected?: boolean;
	onSelect?: (item: Item) => void;
	onDrop?: (slotId: number) => void;
	onUnequip?: (slotId: number) => void;
	onHoverEnter?: (item: Item, ev: MouseEvent) => void;
	onHoverMove?: (ev: MouseEvent) => void;
	onHoverLeave?: () => void;
}

const { slot, item, dragItem, selected, onSelect, onDrop, onUnequip, onHoverEnter, onHoverMove, onHoverLeave }: Props =
	$props();

let over = $state(false);
let hover = $state(false);

const filled = $derived(!!item);
const rc = $derived(item ? rarityColor(item.rarityId) : 'var(--accent)');
const canAccept = $derived(!!dragItem && dragItem.itemCategoryId === slot.category);

const tileBorder = $derived(
	over || selected
		? '#a1c2f7'
		: filled
			? rarityTint(item!.rarityId, 0.6)
			: canAccept
				? 'rgba(161,194,247,0.5)'
				: 'rgba(255,255,255,0.14)'
);

const handleDragOver = (e: DragEvent) => {
	if (canAccept) {
		e.preventDefault();
		over = true;
	}
};

const handleDrop = (e: DragEvent) => {
	e.preventDefault();
	over = false;
	onDrop?.(slot.id);
};
</script>

<style lang="scss">
.equip-row {
	display: flex;
	align-items: center;
	gap: 12px;
}

.row-label {
	width: 64px;
	text-align: right;
	flex-shrink: 0;
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.55);
}

.equip-tile {
	position: relative;
	width: 48px;
	height: 48px;
	flex-shrink: 0;
	border: 1px dashed;
	border-radius: 3px;
	display: flex;
	align-items: center;
	justify-content: center;
	transition: all 120ms;

	&.filled {
		border-style: solid;
		cursor: pointer;
	}

	&.over {
		box-shadow: 0 0 14px rgba(161, 194, 247, 0.45);
	}

	&.selected {
		box-shadow: 0 0 0 1px #a1c2f7;
	}
}

.unequip {
	position: absolute;
	top: 2px;
	right: 2px;
	width: 16px;
	height: 16px;
	border-radius: 2px;
	border: none;
	background: rgba(0, 0, 0, 0.5);
	color: rgba(240, 240, 240, 0.78);
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	font-size: 11px;
	line-height: 1;
	padding: 0;
}

.mod-count {
	position: absolute;
	bottom: 3px;
	right: 4px;
	font-family: 'Geist Mono', monospace;
	font-size: 8px;
	color: rgba(240, 240, 240, 0.55);
}

.row-info {
	min-width: 0;
	flex: 1;
}

.item-name {
	font-size: 13px;
	color: #f0f0f0;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.rarity-tag {
	margin-top: 3px;
	display: flex;
	align-items: center;
	gap: 6px;
}

.rarity-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	flex-shrink: 0;
}

.rarity-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
}

.empty-label {
	font-size: 12px;
	font-style: italic;
	color: rgba(240, 240, 240, 0.4);
}
</style>
