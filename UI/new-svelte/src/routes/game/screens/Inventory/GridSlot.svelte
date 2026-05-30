<div
	class="grid-slot"
	class:selected
	role="button"
	tabindex="0"
	draggable="true"
	style:width="{size}px"
	style:height="{size}px"
	style:background={accentBorders ? hexA(rc, 0.05 + level * 0.012) : 'rgba(255,255,255,0.03)'}
	style:border-color={selected ? accent : borderColor}
	style:box-shadow={boxShadow}
	style:transform={hover ? 'translateY(-1px)' : 'none'}
	ondragstart={handleDragStart}
	ondragend={() => onDragEnd?.()}
	onclick={handleClick}
	ondblclick={() => onToggleEquip?.(item)}
	onkeydown={handleKeydown}
	onmouseenter={(e) => { hover = true; onHoverEnter?.(item, e); }}
	onmousemove={(e) => onHoverMove?.(e)}
	onmouseleave={() => { hover = false; onHoverLeave?.(); }}
>
	<CategoryGlyph cat={item.itemCategoryId} color={item.equipped ? 'rgba(240,240,240,0.95)' : 'rgba(240,240,240,0.6)'} size={Math.round(size * 0.42)} />

	<button
		class="fav-star"
		class:on={item.favorite}
		class:show={hover}
		title={item.favorite ? 'Unfavorite' : 'Favorite'}
		onclick={(e) => { e.stopPropagation(); onToggleFav?.(item); }}
	>
		<svg width="12" height="12" viewBox="0 0 16 16" fill={item.favorite ? '#e8c878' : 'none'} stroke={item.favorite ? '#e8c878' : 'rgba(240,240,240,0.85)'} stroke-width="1.3">
			<path d="M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z" stroke-linejoin="round" />
		</svg>
	</button>

	<div class="cat-corner">
		<CategoryGlyph cat={item.itemCategoryId} color={hexA(catColor, 0.85)} size={10} />
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
import { catAccent, hexA, rarityColor, rarityMeta } from './inventory-view.svelte';
import CategoryGlyph from './CategoryGlyph.svelte';

interface Props {
	item: Item;
	size?: number;
	glow?: boolean;
	accentBorders?: boolean;
	selected?: boolean;
	onSelect?: (item: Item) => void;
	onToggleEquip?: (item: Item) => void;
	onToggleFav?: (item: Item) => void;
	onHoverEnter?: (item: Item, ev: MouseEvent) => void;
	onHoverMove?: (ev: MouseEvent) => void;
	onHoverLeave?: () => void;
	onDragStart?: (item: Item) => void;
	onDragEnd?: () => void;
}

const {
	item,
	size = 64,
	glow = true,
	accentBorders = true,
	selected = false,
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

const accent = '#a1c2f7';
const rc = $derived(rarityColor(item.rarityId));
const level = $derived(rarityMeta(item.rarityId).level);
const glowAmount = $derived(rarityMeta(item.rarityId).glow);
const catColor = $derived(catAccent(item.itemCategoryId));
const modCount = $derived(item.appliedMods.length);

const borderColor = $derived(accentBorders ? hexA(rc, Math.min(0.85, 0.34 + level * 0.09)) : 'rgba(255,255,255,0.14)');
const glowShadow = $derived(glow && glowAmount > 0 ? `0 0 ${5 + glowAmount * 16}px ${hexA(rc, glowAmount * 0.5)}` : 'none');
const boxShadow = $derived(
	selected
		? `0 0 0 1px ${accent}, 0 0 14px ${hexA(accent, 0.4)}`
		: hover
			? `0 0 0 1px ${hexA(rc, 0.6)}, ${glowShadow}`
			: glowShadow
);

const handleClick = (e: MouseEvent) => {
	if (e.metaKey || e.ctrlKey) onToggleEquip?.(item);
	else onSelect?.(item);
};

const handleKeydown = (e: KeyboardEvent) => {
	if (e.key === 'Enter' || e.key === ' ') {
		e.preventDefault();
		onSelect?.(item);
	}
};

const handleDragStart = (e: DragEvent) => {
	e.dataTransfer?.setData('text/plain', String(item.itemId));
	if (e.dataTransfer) e.dataTransfer.effectAllowed = 'move';
	onDragStart?.(item);
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
	transition: box-shadow 120ms, border-color 120ms, transform 120ms;

	&:active {
		cursor: grabbing;
	}
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
}

.cat-corner {
	position: absolute;
	left: 3px;
	bottom: 3px;
	width: 15px;
	height: 15px;
	border-radius: 2px;
	background: rgba(0, 0, 0, 0.35);
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
	background: #bde0b4;
	box-shadow: 0 0 6px rgba(189, 224, 180, 0.7);
}

.mod-count {
	position: absolute;
	top: 3px;
	left: 4px;
	font-family: 'Geist Mono', monospace;
	font-size: 8px;
	letter-spacing: 0.4px;
	color: rgba(240, 240, 240, 0.55);
}
</style>
