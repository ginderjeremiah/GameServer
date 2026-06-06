<!-- Statistics screen — the player's tracked statistics, with a top-level
     By statistic / By entity toggle. The two views cross-link: clicking an
     entity in a stat card pivots to its dossier; clicking a stat in a dossier
     jumps back to its category.

     Values come from GET /api/Statistics; the statistic-type catalogue and the
     entity reference lists come from the in-memory staticData. A brand-new
     player with no recorded statistics gets a friendly empty state. -->
<div class="stats-frame" data-testid="statistics-screen">
	<div class="header">
		<div class="eyebrow">Character · Statistics</div>
		<div class="title-line">
			<h1 class="title">Statistics</h1>
			<span class="sub">tracked totals, broken down by entity</span>
		</div>
	</div>

	<div class="body">
		{#if !view.loading && view.data.isEmpty}
			<StatsEmpty />
		{:else}
			<ViewToggle mode={view.mode} onChange={(m) => view.setMode(m)} />
			{#if view.mode === 'stat'}
				<ByStatisticView {view} />
			{:else}
				<ByEntityView {view} />
			{/if}
		{/if}
	</div>

	{#if view.loading}
		<Loading />
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { ApiRequest } from '$lib/api';
import { Loading } from '$components';
import ViewToggle from './ViewToggle.svelte';
import ByStatisticView from './ByStatisticView.svelte';
import ByEntityView from './ByEntityView.svelte';
import StatsEmpty from './StatsEmpty.svelte';
import { StatisticsView } from './statistics-view.svelte';

const view = new StatisticsView();

onMount(async () => {
	try {
		view.stats = (await ApiRequest.get('Statistics')) ?? [];
	} catch {
		view.stats = [];
	}
	view.loading = false;
});
</script>

<style lang="scss">
.stats-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	position: relative;
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
