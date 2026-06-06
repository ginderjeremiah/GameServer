<!-- Statistics screen — the player's tracked statistics, with a top-level
     By statistic / By entity toggle. The two views cross-link: clicking an
     entity in a stat card pivots to its dossier; clicking a stat in a dossier
     jumps back to its category.

     The displayed values currently come from a temporary mock (statistics-mock.ts);
     wiring real backend data is a tracked follow-up. -->
<div class="stats-frame" data-testid="statistics-screen">
	<div class="header">
		<div class="eyebrow">Character · Statistics</div>
		<div class="title-line">
			<h1 class="title">Statistics</h1>
			<span class="sub">tracked totals, broken down by entity</span>
		</div>
	</div>

	<div class="body">
		<ViewToggle mode={view.mode} onChange={(m) => view.setMode(m)} />
		{#if view.mode === 'stat'}
			<ByStatisticView {view} />
		{:else}
			<ByEntityView {view} />
		{/if}
	</div>
</div>

<script lang="ts">
import ViewToggle from './ViewToggle.svelte';
import ByStatisticView from './ByStatisticView.svelte';
import ByEntityView from './ByEntityView.svelte';
import { StatisticsView } from './statistics-view.svelte';

const view = new StatisticsView();
</script>

<style lang="scss">
.stats-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	padding: 20px 28px 0;
	flex-shrink: 0;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--eyebrow);
	margin-bottom: 6px;
}

.title-line {
	display: flex;
	align-items: baseline;
	gap: 12px;
	flex-wrap: wrap;
}

.title {
	margin: 0;
	font-size: 23px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.sub {
	font-size: 12.5px;
	color: var(--text-tertiary);
}

.body {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	padding: 16px 28px 28px;
}
</style>
