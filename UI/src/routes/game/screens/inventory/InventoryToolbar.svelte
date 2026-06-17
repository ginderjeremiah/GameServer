<div class="toolbar">
	<div class="chips">
		<button class="chip" class:active={view.filterCat == null && !view.favOnly} onclick={() => view.showAll()}>
			All <span class="chip-count">{view.counts.all}</span>
		</button>

		{#each FILTER_CATEGORIES as cat (cat)}
			<button
				class="chip"
				class:active={view.filterCat === cat}
				style:--chip-accent={itemCategoryColor(cat)}
				onclick={() => view.setFilterCat(view.filterCat === cat ? null : cat)}
			>
				<span class="chip-diamond" style:background={itemCategoryColor(cat)}></span>
				{itemCategoryName(cat)} <span class="chip-count">{view.counts.cats[cat] ?? 0}</span>
			</button>
		{/each}

		<button
			class="chip fav"
			class:active={view.favOnly}
			title="Show favorites only"
			onclick={() => view.setFavOnly(!view.favOnly)}
		>
			<FavoriteStar filled={view.favOnly} size={11} />
			Favorites <span class="chip-count">{view.counts.fav}</span>
		</button>
	</div>

	<div class="sort">
		<span class="mono-label">Sort</span>
		<div class="sort-toggle">
			{#each sortKeys as key (key)}
				<button class="sort-option" class:active={view.sort === key} onclick={() => view.setSort(key)}>
					{SORTS[key].label}
				</button>
			{/each}
		</div>
	</div>
</div>

<script lang="ts">
import { itemCategoryColor, itemCategoryName } from '$lib/common';
import { FILTER_CATEGORIES, SORTS, type InventoryView, type SortKey } from './inventory-view.svelte';
import FavoriteStar from './FavoriteStar.svelte';

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
