<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
	class="grid-slot"
	class:selected
	style:width="{size}px"
	style:height="{size}px"
	style:background={bg}
	style:border-color={borderColor}
	style:box-shadow={boxShadow}
	style:transform={hover ? 'translateY(-1px)' : 'none'}
	onmouseenter={(e) => {
		hover = true;
		onHoverEnter?.(item, e);
	}}
	onmousemove={(e) => onHoverMove?.(e)}
	onmouseleave={() => {
		hover = false;
		onHoverLeave?.();
	}}
>
	{#if item.iconPath}
		<img class="item-icon" src={item.iconPath} alt="" />
	{:else}
		<CategoryGlyph
			cat={item.itemCategoryId}
			color={item.equipped ? tintColor('var(--text-primary)', 0.95) : tintColor('var(--text-primary)', 0.6)}
			size={Math.round(size * 0.42)}
		/>
	{/if}

	<!-- Full-bleed primary action: plain activate selects, modifier/double activate equips.
	     A real <button> gives native keyboard activation (modifier state rides the click). It also
	     surfaces the item tooltip on keyboard focus (the hover handlers on the tile cover the mouse). -->
	<OverlayButton
		label={item.name}
		draggable
		{describedById}
		onActivate={handleActivate}
		onDoubleClick={() => onToggleEquip?.(item)}
		onDragStart={handleDragStart}
		onDragEnd={() => onDragEnd?.()}
		onFocus={handleFocus}
		onBlur={() => onHoverLeave?.()}
	/>

	<button
		type="button"
		class="fav-star"
		class:on={item.favorite}
		class:show={hover}
		title={item.favorite ? 'Unfavorite' : 'Favorite'}
		aria-label={item.favorite ? 'Unfavorite' : 'Favorite'}
		onclick={() => onToggleFav?.(item)}
	>
		<FavoriteStar
			filled={item.favorite}
			stroke={item.favorite ? 'var(--category-accessory)' : tintColor('var(--text-primary)', 0.85)}
		/>
	</button>

	<div class="cat-corner">
		<CategoryGlyph cat={item.itemCategoryId} color={tintColor(catColor, 0.85)} size={10} />
	</div>

	{#if item.equipped}
		<div class="equipped-marker"></div>
	{/if}

	{#if modCount > 0}
		<span class="mod-count">{modCount}◈</span>
	{/if}
</div>

<script lang="ts">
import type { Item } from '$lib/battle';
import { itemCategoryColor, rarityColor, rarityGlow, rarityLevel, rarityTint, tintColor } from '$lib/common';
import { focusAnchor, type TooltipAnchor } from '$stores/tooltip.svelte';
import CategoryGlyph from './CategoryGlyph.svelte';
import FavoriteStar from './FavoriteStar.svelte';
import OverlayButton from '$components/OverlayButton.svelte';

interface Props {
	item: Item;
	size?: number;
	glow?: boolean;
	selected?: boolean;
	/** Tooltip container id wired onto the primary action's `aria-describedby` for screen readers. */
	describedById?: string;
	onSelect?: (item: Item) => void;
	onToggleEquip?: (item: Item) => void;
	onToggleFav?: (item: Item) => void;
	/** Fired on hover (cursor anchor) and keyboard focus (the tile's box) so both surface the tooltip. */
	onHoverEnter?: (item: Item, anchor: TooltipAnchor) => void;
	onHoverMove?: (ev: MouseEvent) => void;
	onHoverLeave?: () => void;
	onDragStart?: (item: Item) => void;
	onDragEnd?: () => void;
}

const {
	item,
	size = 64,
	glow = true,
	selected = false,
	describedById,
	onSelect,
	onToggleEquip,
	onToggleFav,
	onHoverEnter,
	onHoverMove,
	onHoverLeave,
	onDragStart,
	onDragEnd
}: Props = $props();

let hover = $state(false);

const rc = $derived(rarityColor(item.rarityId));
const level = $derived(rarityLevel(item.rarityId));
const catColor = $derived(itemCategoryColor(item.itemCategoryId));
const modCount = $derived(item.appliedMods.length);

const bg = $derived(rarityTint(item.rarityId, 0.05 + level * 0.012));
const borderColor = $derived(
	selected ? 'var(--accent)' : rarityTint(item.rarityId, Math.min(0.85, 0.34 + level * 0.09))
);
// Glow intensity is a themeable CSS var, so the blur radius and alpha are derived in-CSS via calc().
const glowShadow = $derived(
	glow
		? `0 0 calc(5px + ${rarityGlow(item.rarityId)} * 16px) color-mix(in srgb, ${rc} calc(${rarityGlow(item.rarityId)} * 50%), transparent)`
		: '0 0 0 transparent'
);
const boxShadow = $derived(
	selected
		? '0 0 0 1px var(--accent), 0 0 14px color-mix(in srgb, var(--accent) 40%, transparent)'
		: hover
			? `0 0 0 1px ${rarityTint(item.rarityId, 0.6)}, ${glowShadow}`
			: glowShadow
);

// A modifier-held activate (⌘/Ctrl-click, or ⌘/Ctrl + keyboard activation, since the modifier
// state rides the native button's click) equips; a plain activate selects.
const handleActivate = (e: MouseEvent) => {
	if (e.metaKey || e.ctrlKey) {
		onToggleEquip?.(item);
	} else {
		onSelect?.(item);
	}
};

const handleDragStart = (e: DragEvent) => {
	e.dataTransfer?.setData('text/plain', String(item.itemId));
	if (e.dataTransfer) {
		e.dataTransfer.effectAllowed = 'move';
	}
	onDragStart?.(item);
};

// Surface the tooltip on keyboard focus, anchored off the tile's box. A mouse click is left to the
// hover handlers (already tracking the cursor) so the tooltip doesn't jump on click.
const handleFocus = (e: FocusEvent) => {
	const anchor = focusAnchor(e);
	if (anchor) {
		onHoverEnter?.(item, anchor);
	}
};
</script>

<style lang="scss">
.grid-slot {
	position: relative;
	flex-shrink: 0;
	border: 1px solid;
	border-radius: 3px;
	cursor: grab;
	display: flex;
	align-items: center;
	justify-content: center;
	overflow: hidden;
	transition:
		box-shadow 120ms,
		border-color 120ms,
		transform 120ms;

	&:active {
		cursor: grabbing;
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

.fav-star {
	position: absolute;
	top: 3px;
	right: 3px;
	width: 18px;
	height: 18px;
	padding: 0;
	border: none;
	background: transparent;
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	opacity: 0;
	transition: opacity 120ms;
	z-index: 3;

	&.show {
		opacity: 0.75;
	}

	&.on {
		opacity: 1;
	}

	// Keyboard parity with hover: a focused star is fully visible, and tabbing onto the slot's
	// primary action hints the star too — a keyboard user otherwise lands on an invisible control.
	&:focus-visible {
		opacity: 1;
	}
}

.grid-slot:focus-within .fav-star {
	opacity: 0.75;
}

.cat-corner {
	position: absolute;
	left: 3px;
	bottom: 3px;
	width: 15px;
	height: 15px;
	border-radius: 2px;
	background: color-mix(in srgb, var(--black) 35%, transparent);
	display: flex;
	align-items: center;
	justify-content: center;
}

.equipped-marker {
	position: absolute;
	right: 5px;
	bottom: 5px;
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	background: var(--success);
	box-shadow: 0 0 6px color-mix(in srgb, var(--success) 70%, transparent);
}

.mod-count {
	position: absolute;
	top: 3px;
	left: 4px;
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 0.4px;
	color: var(--text-tertiary);
}
</style>
