<!-- The searchable entity list for the "by entity" view. Selecting an entity
     swaps the dossier on the right. -->
<div class="picker" data-testid="entity-picker">
	<div class="search">
		<svg
			class="search-icon"
			width="13"
			height="13"
			viewBox="0 0 14 14"
			fill="none"
			stroke="var(--text-muted)"
			stroke-width="1.4"
		>
			<circle cx="6" cy="6" r="4.2" />
			<path d="M9.2 9.2L12 12" stroke-linecap="round" />
		</svg>
		<input
			type="text"
			value={view.query}
			oninput={(e) => view.setQuery(e.currentTarget.value)}
			placeholder="Search {statKindPlural(view.entKind).toLowerCase()}…"
			aria-label="Search {statKindPlural(view.entKind).toLowerCase()}"
		/>
	</div>

	<div class="list">
		{#each view.filteredEntities as entity (entity.id)}
			{@const on = view.selectedEntity?.id === entity.id}
			<button
				type="button"
				class="entity"
				class:on
				data-testid="entity-{entity.id}"
				onclick={() => view.setEntId(entity.id)}
			>
				<StatGlyph kind={view.entKind} size={15} color={on ? statKindColor(view.entKind) : undefined} />
				<span class="name">{entity.name}</span>
				{#if view.entKind === 'enemy' && entity.boss}
					<span class="sub boss">Boss</span>
				{:else if view.entKind === 'zone' && entity.zoneNum != null}
					<span class="sub">Z{entity.zoneNum}</span>
				{/if}
			</button>
		{/each}
		{#if view.filteredEntities.length === 0}
			<span class="empty">No matches.</span>
		{/if}
	</div>
</div>

<script lang="ts">
import StatGlyph from './StatGlyph.svelte';
import type { StatisticsView } from './statistics-view.svelte';
import { statKindColor, statKindPlural } from './statistics-display';

interface Props {
	view: StatisticsView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.picker {
	display: flex;
	flex-direction: column;
	min-height: 0;
	border-right: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
	padding-right: 16px;
}

.search {
	position: relative;
	margin-bottom: 10px;
}

.search-icon {
	position: absolute;
	left: 9px;
	top: 9px;
}

input {
	width: 100%;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 4px;
	padding: 8px 10px 8px 28px;
	color: var(--text-primary);
	font-family: var(--sans);
	font-size: 12.5px;
	outline: none;

	&:focus {
		border-color: color-mix(in srgb, var(--accent) 55%, transparent);
	}
}

.list {
	overflow: auto;
	flex: 1;
	min-height: 0;
}

.entity {
	width: 100%;
	text-align: left;
	display: flex;
	align-items: center;
	gap: 9px;
	padding: 8px 10px;
	margin-bottom: 2px;
	border-radius: 4px;
	cursor: pointer;
	background: transparent;
	border: 1px solid transparent;
	transition: background 120ms;

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.on {
		background: color-mix(in srgb, var(--accent) 10%, transparent);
		border-color: color-mix(in srgb, var(--accent) 40%, transparent);

		.name {
			color: var(--text-primary);
		}
	}
}

.name {
	flex: 1;
	font-size: 13px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.sub {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.6px;
	color: var(--text-muted);

	&.boss {
		color: var(--category-accessory);
		border: 1px solid color-mix(in srgb, var(--category-accessory) 40%, transparent);
		border-radius: 2px;
		padding: 1px 4px;
		text-transform: uppercase;
		letter-spacing: 0.8px;
		font-size: 8px;
	}
}

.empty {
	display: block;
	padding: 10px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
}
</style>
