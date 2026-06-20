<!-- Codex screen — a read-only reference glossary (bestiary / atlas / skill catalogue). The Enemies
     and Zones tabs are built; Skills is a placeholder. Per-entity player statistics live in the enemy
     dossier here, and the Statistics screen deep-links an enemy into it via the navigation store.
     Ported from the `Glossary.dc.html` Claude Design handoff onto live reference/runtime data. -->
<div class="codex" data-testid="codex-screen">
	<div class="header">
		<span class="diamond" aria-hidden="true"></span>
		<h1 class="title">Codex</h1>
	</div>

	<CodexTabBar {view} />

	<div class="content">
		{#if view.tab === 'enemies'}
			<EnemiesTab {view} />
		{:else if view.tab === 'zones'}
			<ZonesTab {view} />
		{:else}
			<ComingSoonPanel label="Skills" accent="var(--attr-intellect)" />
		{/if}
	</div>
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { navigation, playerChallenges, statistics } from '$stores';
import { CodexView, type CodexNavPayload } from './codex-view.svelte';
import CodexTabBar from './CodexTabBar.svelte';
import ComingSoonPanel from './ComingSoonPanel.svelte';
import EnemiesTab from './EnemiesTab.svelte';
import ZonesTab from './ZonesTab.svelte';

// Consume any deep-link payload (e.g. an enemy handed over from the Statistics screen) once.
const view = new CodexView(navigation.consumePayload<CodexNavPayload>());

onMount(async () => {
	// Force-refresh the player's statistics (the dossier's "your record"); challenge progress is
	// loaded once at boot, so a plain load is enough.
	await Promise.all([statistics.load(true), playerChallenges.load()]);
	view.stats = statistics.stats;
	view.statsError = statistics.error;
	view.statsLoading = false;
});
</script>

<style lang="scss">
.codex {
	height: 100%;
	display: flex;
	flex-direction: column;
	background: linear-gradient(180deg, var(--panel) 0%, var(--surface) 64%);
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	display: flex;
	align-items: center;
	gap: 12px;
	padding: 24px 30px 16px;
	flex: none;
}

.diamond {
	width: 10px;
	height: 10px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
	flex: none;
}

.title {
	margin: 0;
	font-size: 28px;
	font-weight: 400;
	letter-spacing: -0.4px;
	line-height: 1;
}

.content {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
}
</style>
