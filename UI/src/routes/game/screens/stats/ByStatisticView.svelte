<!-- "By statistic" view: category tabs over a grid of stat cards. Clicking an
     entity row in a card pivots to that entity's dossier. -->
<div class="by-stat">
	<UnderlineTabs {tabs} active={view.statCat} onChange={(k) => view.setStatCat(k as StatCategory | 'all')} />
	<div class="scroll">
		<div class="grid" data-testid="stat-card-grid">
			{#each view.shownStats as stat (stat.id)}
				<StatCard summary={view.data.summaryFor(stat.id)} {stat} onPickEntity={(kind, id) => view.goEntity(kind, id)} />
			{/each}
		</div>
	</div>
</div>

<script lang="ts">
import UnderlineTabs, { type Tab } from './UnderlineTabs.svelte';
import StatCard from './StatCard.svelte';
import { STAT_CATEGORIES, type StatCategory, type StatisticsView } from './statistics-view.svelte';
import { statCategoryColor } from './statistics-display';

interface Props {
	view: StatisticsView;
}

let { view }: Props = $props();

// Counts come from the live catalogue (server-sourced), so the tabs stay in sync
// with whichever statistic types the backend exposes.
const tabs = $derived<Tab[]>([
	{ key: 'all', label: 'All', color: 'var(--accent)', count: view.data.statTypes.length },
	...STAT_CATEGORIES.map((c) => ({
		key: c.key,
		label: c.label,
		color: statCategoryColor(c.key),
		count: view.data.statsInCategory(c.key).length
	}))
]);
</script>

<style lang="scss">
.by-stat {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
}

.scroll {
	flex: 1;
	min-height: 0;
	overflow: auto;
	padding-right: 6px;
	margin-top: 16px;
}

.grid {
	display: grid;
	grid-template-columns: repeat(2, 1fr);
	gap: 14px;
	padding-bottom: 8px;
}

@media (max-width: 760px) {
	.grid {
		grid-template-columns: 1fr;
	}
}
</style>
