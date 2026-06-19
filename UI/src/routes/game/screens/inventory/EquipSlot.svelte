<div class="equip-row">
	<div class="row-label">{slot.label}</div>

	<!-- The tile is a presentational drop target; only a filled slot carries an interactive
	     (focusable) select button, so empty slots stay out of the tab order. -->
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<div
		class="equip-tile"
		class:filled
		class:over
		class:selected
		class:can-accept={canAccept}
		style:border-color={tileBorder}
		style:background={over
			? tintColor('var(--accent)', 0.14)
			: item
				? rarityTint(item.rarityId, 0.07)
				: tintColor('var(--white)', 0.03)}
		ondragover={handleDragOver}
		ondragleave={() => (over = false)}
		ondrop={handleDrop}
		onmouseenter={(e) => {
			hover = true;
			if (item) {
				onHoverEnter?.(item, e);
			}
		}}
		onmousemove={(e) => item && onHoverMove?.(e)}
		onmouseleave={() => {
			hover = false;
			onHoverLeave?.();
		}}
	>
		{#if item}
			{#if item.iconPath}
				<img class="item-icon" src={item.iconPath} alt="" />
			{:else}
				<CategoryGlyph cat={item.itemCategoryId} color={itemCategoryColor(item.itemCategoryId)} size={40} />
			{/if}
			<OverlayButton
				label={item.name}
				{describedById}
				onActivate={() => onSelect?.(item)}
				onFocus={handleFocus}
				onBlur={() => onHoverLeave?.()}
			/>
			<button
				type="button"
				class="unequip"
				class:show={hover}
				title="Unequip"
				aria-label="Unequip {item.name}"
				onclick={() => onUnequip?.(slot.id)}>×</button
			>
			{#if item.appliedMods.length}
				<span class="mod-count">{item.appliedMods.length}◈</span>
			{/if}
		{:else}
			<CategoryGlyph cat={slot.category} color={tintColor('var(--text-primary)', 0.18)} size={38} />
		{/if}
	</div>

	<div class="row-info">
		{#if item}
			<div class="item-name">{item.name}</div>
			<RarityTag rarityId={item.rarityId} style="margin-top: 3px" />
		{:else}
			<div class="empty-label">Empty</div>
		{/if}
	</div>
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import { itemCategoryColor, rarityTint, tintColor } from '$lib/common';
import { focusAnchor, type TooltipAnchor } from '$stores/tooltip.svelte';
import RarityTag from '$components/RarityTag.svelte';
import CategoryGlyph from './CategoryGlyph.svelte';
import OverlayButton from '$components/OverlayButton.svelte';
import { type EquipSlotDef } from './inventory-view.svelte';

interface Props {
	slot: EquipSlotDef;
	item?: Item;
	dragItem?: Item | null;
	selected?: boolean;
	/** Tooltip container id wired onto the select action's `aria-describedby` for screen readers. */
	describedById?: string;
	onSelect?: (item: Item) => void;
	onDrop?: (slotId: number) => void;
	onUnequip?: (slotId: number) => void;
	/** Fired on hover (cursor anchor) and keyboard focus (the tile's box) so both surface the tooltip. */
	onHoverEnter?: (item: Item, anchor: TooltipAnchor) => void;
	onHoverMove?: (ev: MouseEvent) => void;
	onHoverLeave?: () => void;
}

const {
	slot,
	item,
	dragItem,
	selected,
	describedById,
	onSelect,
	onDrop,
	onUnequip,
	onHoverEnter,
	onHoverMove,
	onHoverLeave
}: Props = $props();

let over = $state(false);
let hover = $state(false);

const filled = $derived(!!item);
const canAccept = $derived(!!dragItem && dragItem.itemCategoryId === slot.category);

const tileBorder = $derived(
	over || selected
		? 'var(--accent)'
		: item
			? rarityTint(item.rarityId, 0.6)
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

// Surface the tooltip on keyboard focus, anchored off the tile's box. A mouse click is left to the
// hover handlers (already tracking the cursor) so the tooltip doesn't jump on click.
const handleFocus = (e: FocusEvent) => {
	const anchor = focusAnchor(e);
	if (item && anchor) {
		onHoverEnter?.(item, anchor);
	}
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
	width: 64px;
	height: 64px;
	flex-shrink: 0;
	border: 1px dashed;
	border-radius: 3px;
	display: flex;
	align-items: center;
	justify-content: center;
	overflow: hidden;
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

.item-icon {
	position: absolute;
	inset: 0;
	width: 100%;
	height: 100%;
	object-fit: cover;
	opacity: 0.92;
}

.unequip {
	position: absolute;
	top: 2px;
	right: 2px;
	// Above the full-bleed select button so it stays clickable.
	z-index: 2;
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
	// Always in the DOM (so keyboard/touch can reach it); revealed on hover or keyboard focus,
	// mirroring GridSlot's favorite star.
	opacity: 0;
	transition: opacity 120ms;

	&.show,
	&:focus-visible {
		opacity: 1;
	}
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

.empty-label {
	font-size: 12px;
	font-style: italic;
	color: var(--text-muted);
}
</style>
