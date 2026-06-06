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
			? tintColor('var(--accent)', 0.14)
			: filled
				? rarityTint(item!.rarityId, 0.07)
				: tintColor('var(--white)', 0.03)}
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
			<CategoryGlyph cat={item!.itemCategoryId} color={itemCategoryColor(item!.itemCategoryId)} size={20} />
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
			<CategoryGlyph cat={slot.category} color={tintColor('var(--text-primary)', 0.18)} size={19} />
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
import { itemCategoryColor, rarityColor, rarityLabel, rarityTint, tintColor } from '$lib/common';
import CategoryGlyph from './CategoryGlyph.svelte';
import { type EquipSlotDef } from './inventory-view.svelte';

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
		? 'var(--accent)'
		: filled
			? rarityTint(item!.rarityId, 0.6)
			: canAccept
				? tintColor('var(--accent)', 0.5)
				: 'var(--border-light)'
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
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-tertiary);
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
		box-shadow: 0 0 14px color-mix(in srgb, var(--accent) 45%, transparent);
	}

	&.selected {
		box-shadow: 0 0 0 1px var(--accent);
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
	background: color-mix(in srgb, var(--black) 50%, transparent);
	color: var(--text-secondary);
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
	font-family: var(--mono);
	font-size: 8px;
	color: var(--text-tertiary);
}

.row-info {
	min-width: 0;
	flex: 1;
}

.item-name {
	font-size: 13px;
	color: var(--text-primary);
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
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
}

.empty-label {
	font-size: 12px;
	font-style: italic;
	color: var(--text-muted);
}
</style>
