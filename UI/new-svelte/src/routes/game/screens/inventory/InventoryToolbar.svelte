<div class="toolbar">
	<div class="chips">
		<button
			class="chip"
			class:active={view.filterCat == null && !view.favOnly}
			onclick={() => {
				view.filterCat = null;
				view.favOnly = false;
			}}
		>
			All <span class="chip-count">{view.counts.all}</span>
		</button>

		{#each FILTER_CATEGORIES as cat (cat)}
			<button
				class="chip"
				class:active={view.filterCat === cat}
				style:--chip-accent={itemCategoryColor(cat)}
				onclick={() => (view.filterCat = view.filterCat === cat ? null : cat)}
			>
				<span class="chip-diamond" style:background={itemCategoryColor(cat)}></span>
				{itemCategoryName(cat)} <span class="chip-count">{view.counts.cats[cat] ?? 0}</span>
			</button>
		{/each}

		<button
			class="chip fav"
			class:active={view.favOnly}
			title="Show favorites only"
			onclick={() => (view.favOnly = !view.favOnly)}
		>
			<svg
				width="11"
				height="11"
				viewBox="0 0 16 16"
				style:fill={view.favOnly ? 'var(--category-accessory)' : 'none'}
				style:stroke="var(--category-accessory)"
				stroke-width="1.3"
			>
				<path d="M8 1.6l1.9 3.9 4.3.6-3.1 3 .7 4.3L8 11.4 4.3 13.4l.7-4.3-3.1-3 4.3-.6z" stroke-linejoin="round" />
			</svg>
			Favorites <span class="chip-count">{view.counts.fav}</span>
		</button>
	</div>

	<div class="sort">
		<span class="mono-label">Sort</span>
		<div class="sort-toggle">
			{#each sortKeys as key (key)}
				<button class="sort-option" class:active={view.sort === key} onclick={() => (view.sort = key)}>
					{SORTS[key].label}
				</button>
			{/each}
		</div>
	</div>
</div>

<script lang="ts">
import { itemCategoryColor, itemCategoryName } from '$lib/common';
import { FILTER_CATEGORIES, SORTS, type InventoryView, type SortKey } from './inventory-view.svelte';

const { view }: { view: InventoryView } = $props();
const sortKeys = Object.keys(SORTS) as SortKey[];
</script>

<style lang="scss">
.toolbar {
	display: flex;
	align-items: center;
	gap: 14px;
	flex-wrap: wrap;
}

.chips {
	display: flex;
	align-items: center;
	gap: 5px;
	flex-wrap: wrap;
}

.chip {
	display: flex;
	align-items: center;
	gap: 6px;
	padding: 4px 10px;
	font-family: Geist, sans-serif;
	font-size: 11.5px;
	line-height: 1.2;
	background: transparent;
	color: var(--text-tertiary);
	border: 1px solid var(--border-subtle);
	border-radius: 2px;
	cursor: pointer;
	white-space: nowrap;
	transition: all 120ms;

	&:hover {
		background: color-mix(in srgb, var(--white) 6%, transparent);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 16%, transparent);
		color: var(--text-primary);
		border-color: color-mix(in srgb, var(--accent) 55%, transparent);
	}

	&.fav.active {
		background: color-mix(in srgb, var(--category-accessory) 16%, transparent);
		border-color: color-mix(in srgb, var(--category-accessory) 55%, transparent);
	}
}

.chip-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	flex-shrink: 0;
}

.chip-count {
	font-family: var(--mono);
	font-size: 9.5px;
	opacity: 0.6;
}

.sort {
	display: flex;
	align-items: center;
	gap: 7px;
	margin-left: auto;
}

.mono-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.sort-toggle {
	display: flex;
	border: 1px solid var(--border-subtle);
	border-radius: 2px;
	overflow: hidden;
}

.sort-option {
	padding: 4px 11px;
	font-family: Geist, sans-serif;
	font-size: 11.5px;
	cursor: pointer;
	border: none;
	background: transparent;
	color: var(--text-tertiary);
	transition: all 120ms;

	&:not(:first-child) {
		border-left: 1px solid var(--border-subtle);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 16%, transparent);
		color: var(--text-primary);
	}
}
</style>
