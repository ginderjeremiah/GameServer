<!-- The zone dossier (right panel): a section-accented header (Zone + progression seal + name), the
     level band / spawn-pool meta, the zone boss card, the spawn table and the unlock condition. The
     boss card and spawn rows cross-link into the enemy dossier. -->
<DossierShell
	selectedItem={view.selectedZone}
	testid="codex-zone-dossier"
	accent="var(--accent)"
	kind="Zone"
	name={view.selectedZone?.name ?? ''}
	description={view.selectedZone?.description}
>
	{#snippet headExtra()}
		<span class="spacer"></span>
		<span class="seal" data-testid="codex-zone-status" style:--status-color={zoneStatusColor(view.selectedZoneStatus)}
			>{ZONE_STATUS_LABELS[view.selectedZoneStatus]}</span
		>
	{/snippet}

	<div class="body">
		<div class="meta">
			<div class="meta-card">
				<div class="meta-label">Level Range</div>
				<div class="meta-val">{view.zoneBand}</div>
			</div>
			<div class="meta-card">
				<div class="meta-label">Spawn Pool</div>
				<div class="meta-val">{view.zoneSpawnCount} {view.zoneSpawnCount === 1 ? 'enemy' : 'enemies'}</div>
			</div>
		</div>

		{#if view.zoneBoss}
			{@const boss = view.zoneBoss}
			<div class="section">
				<div class="section-label">Zone boss</div>
				<button
					type="button"
					class="boss-card"
					data-testid="codex-zone-boss"
					onclick={() => view.openEnemy(boss.enemyId)}
				>
					<span class="boss-mark"></span>
					<span class="boss-text">
						<span class="boss-name">{boss.name}</span>
						<span class="boss-meta">Boss · LVL {boss.level}</span>
					</span>
					<span class="chev" aria-hidden="true">›</span>
				</button>
			</div>
		{/if}

		<div class="section">
			<ZoneSpawnPanel {view} />
		</div>

		<div class="section">
			<div class="section-label">Unlock condition</div>
			{#if view.zoneUnlock}
				{@const unlock = view.zoneUnlock}
				<div class="unlock" class:sealed={unlock.sealed}>
					<span class="unlock-text">
						<span class="unlock-lead">Complete challenge</span>
						<span class="unlock-name">{unlock.challengeName}</span>
					</span>
					<span class="unlock-chip">{unlock.sealed ? 'Sealed' : 'Met'}</span>
				</div>
			{:else}
				<div class="unlock open">
					<span class="unlock-text">
						<span class="unlock-name">Always open</span>
						<span class="unlock-lead">No challenge gates this zone</span>
					</span>
				</div>
			{/if}
		</div>

		<div class="section">
			<StatisticsPanel
				stats={view.zoneStatistics}
				loading={view.statsLoading}
				error={view.statsError}
				emptyMessage="No statistics recorded for this zone yet."
				testid="codex-zone-stats"
			/>
		</div>
	</div>
</DossierShell>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { ZONE_STATUS_LABELS, zoneStatusColor } from './codex-display';
import DossierShell from './DossierShell.svelte';
import ZoneSpawnPanel from './ZoneSpawnPanel.svelte';
import StatisticsPanel from './StatisticsPanel.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
@use '$styles/codex-dossier' as dossier;

.spacer {
	flex: 1;
}

.seal {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--status-color);
	border: 1px solid color-mix(in srgb, var(--status-color) 45%, transparent);
	border-radius: 10px;
	padding: 2px 9px;
}

.body {
	@include dossier.stacked-body;
}

.meta {
	@include dossier.meta-row;
}

.meta-card {
	@include dossier.meta-card;
}

.meta-label {
	@include dossier.meta-label;
}

.meta-val {
	@include dossier.meta-val;
}

.section-label {
	@include dossier.section-label;
}

.boss-card {
	display: flex;
	align-items: center;
	gap: 11px;
	width: 100%;
	text-align: left;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-left: 2px solid var(--boss-accent);
	border-radius: 5px;
	padding: 10px 12px;
	cursor: pointer;

	&:hover {
		background: color-mix(in srgb, var(--boss-accent) 8%, var(--panel));

		.chev {
			color: var(--boss-accent);
			transform: translateX(2px);
		}
	}
}

.boss-mark {
	width: 9px;
	height: 9px;
	transform: rotate(45deg);
	background: var(--boss-accent);
	box-shadow: 0 0 6px color-mix(in srgb, var(--boss-accent) 50%, transparent);
	flex: none;
}

.boss-text {
	flex: 1;
	min-width: 0;
}

.boss-name {
	display: block;
	font-size: 13.5px;
	color: var(--text-primary);
}

.boss-meta {
	display: block;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: var(--boss-accent);
	margin-top: 2px;
}

.chev {
	font-size: 16px;
	color: var(--text-muted);
	flex: none;
	transition:
		transform 0.12s ease,
		color 0.12s ease;
}

.unlock {
	display: flex;
	align-items: center;
	gap: 10px;
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-left: 2px solid var(--success);
	border-radius: 4px;
	padding: 10px 12px;

	&.sealed {
		border-left-color: var(--text-muted);
	}

	&.open {
		border-left-color: var(--accent);
	}
}

.unlock-text {
	flex: 1;
	min-width: 0;
}

.unlock-lead {
	display: block;
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.unlock-name {
	display: block;
	font-size: 12.5px;
	color: var(--text-primary);
	margin-top: 2px;
}

.unlock.open .unlock-name {
	margin-top: 0;
	margin-bottom: 2px;
}

.unlock-chip {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--success);
	flex: none;
}

.unlock.sealed .unlock-chip {
	color: var(--text-muted);
}
</style>
