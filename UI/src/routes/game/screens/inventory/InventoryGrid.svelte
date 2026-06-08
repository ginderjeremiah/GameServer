<div class="grid-panel">
	<div class="grid-toolbar">
		<InventoryToolbar {view} />
	</div>

	<div class="grid-scroll">
		<div class="grid">
			{#each slice as item (item.itemId)}
				<GridSlot
					{item}
					selected={view.selectedId === item.itemId}
					onSelect={(it) => view.select(view.selectedId === it.itemId ? null : it.itemId)}
					onToggleEquip={(it) => view.toggleEquip(it)}
					onToggleFav={(it) => view.toggleFavorite(it.itemId)}
					onHoverEnter={handleHoverEnter}
					onHoverMove={handleHoverMove}
					onHoverLeave={handleHoverLeave}
					onDragStart={(it) => {
						view.dragItemId = it.itemId;
						handleHoverLeave();
					}}
					onDragEnd={() => (view.dragItemId = null)}
				/>
			{/each}
			{#if slice.length === 0}
				<div class="empty">No items match this filter.</div>
			{/if}
		</div>
	</div>

	<div class="grid-footer">
		{#if pages <= 1}
			<span class="mono-label">{view.visible.length} items</span>
		{:else}
			<span class="mono-label">{view.visible.length} items</span>
			<div class="pager">
				<button class="page-btn" disabled={pageClamped === 0} onclick={() => (view.page = Math.max(0, pageClamped - 1))}
					>‹</button
				>
				<span class="page-indicator">{pageClamped + 1} / {pages}</span>
				<button
					class="page-btn"
					disabled={pageClamped === pages - 1}
					onclick={() => (view.page = Math.min(pages - 1, pageClamped + 1))}>›</button
				>
			</div>
		{/if}
	</div>

	<ItemTooltip bind:this={tooltip} item={tooltipItem} />
</div>

<script lang="ts">
import GridSlot from './GridSlot.svelte';
import InventoryToolbar from './InventoryToolbar.svelte';
import ItemTooltip from './ItemTooltip.svelte';
import { registerTooltipComponent, type TooltipComponent } from '$stores';
import type { Item } from '$lib/battle';
import type { InventoryView } from './inventory-view.svelte';

const { view }: { view: InventoryView } = $props();

const perPage = 48;

const pages = $derived(Math.max(1, Math.ceil(view.visible.length / perPage)));
const pageClamped = $derived(Math.min(view.page, pages - 1));
const slice = $derived(view.visible.slice(pageClamped * perPage, pageClamped * perPage + perPage));

// Reset to the first page whenever the filter/sort changes.
$effect(() => {
	view.page = 0;
});

let tooltip = $state<TooltipComponent>();
let tooltipItem = $state<Item>();

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleHoverEnter = (item: Item, ev: MouseEvent) => {
	if (view.selectedId != null || view.dragItemId != null) {
		return;
	}
	tooltipItem = item;
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
	showTooltip();
};
const handleHoverMove = (ev: MouseEvent) => setTooltipPosition({ x: ev.clientX, y: ev.clientY });
const handleHoverLeave = () => {
	tooltipItem = undefined;
	hideTooltip();
};
</script>

<style lang="scss">
.grid-panel {
	flex: 1;
	min-width: 0;
	position: relative;
	display: flex;
	flex-direction: column;
	background: color-mix(in srgb, var(--surface) 50%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	overflow: hidden;
}

.grid-toolbar {
	padding: 14px 16px 10px;
	border-bottom: 1px solid var(--border-subtle);
}

.grid-scroll {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px;
}

.grid {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	align-content: flex-start;
}

.empty {
	width: 100%;
	text-align: center;
	padding: 40px;
	color: var(--text-muted);
	font-size: 13px;
}

.grid-footer {
	padding: 10px 16px;
	border-top: 1px solid var(--border-subtle);
	display: flex;
	align-items: center;
	justify-content: space-between;
}

.mono-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.pager {
	display: flex;
	align-items: center;
	gap: 8px;
}

.page-btn {
	width: 24px;
	height: 22px;
	border: 1px solid var(--border-subtle);
	border-radius: 2px;
	cursor: pointer;
	background: transparent;
	color: var(--text-secondary);
	font-family: var(--mono);
	font-size: 12px;

	&:disabled {
		opacity: 0.3;
		cursor: default;
	}
}

.page-indicator {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-tertiary);
	min-width: 44px;
	text-align: center;
}
</style>
