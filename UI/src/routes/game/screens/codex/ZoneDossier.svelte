<!-- The zone dossier (right panel): a zone-accented header (kind + name + level band + status), the
     zone boss card, the spawn table and the unlock gate. The boss card and spawn rows deep-link into
     the enemy dossier on the Enemies tab. -->
{#if view.selectedZone}
	<div class="dossier" data-testid="codex-zone-dossier">
		<div class="head">
			<div class="kind-line">
				<span class="kind-dot"></span>
				<span class="kind">Zone</span>
			</div>
			<div class="name">{view.selectedZone.name}</div>
			<div class="meta">
				<span class="band">Levels {view.selectedZoneBand}</span>
				<span class="sep">·</span>
				<span class="spawn-count">{view.zoneSpawnRows.length} spawns</span>
				<span class="status" style:--zone-status={zoneStatusColor(view.selectedZoneStatus)}>
					{zoneStatusLabel(view.selectedZoneStatus)}
				</span>
			</div>
		</div>

		<div class="body">
			<section class="block">
				<div class="block-label">Zone boss</div>
				{#if view.zoneBoss}
					{@const boss = view.zoneBoss}
					<button type="button" class="boss" data-testid="codex-zone-boss" onclick={() => view.openEnemy(boss.id)}>
						<span class="boss-mark"></span>
						<span class="boss-text">
							<span class="boss-name">{boss.name}</span>
							<span class="boss-tier">Boss · LVL {boss.level}</span>
						</span>
						<span class="chev" aria-hidden="true">›</span>
					</button>
				{:else}
					<div class="empty">No boss guards this zone.</div>
				{/if}
			</section>

			<section class="block">
				<ZoneSpawnsPanel {view} />
			</section>

			<section class="block">
				<div class="block-label">Unlock condition</div>
				{#if !view.zoneUnlock.gated}
					<div class="unlock open" data-testid="codex-zone-unlock">Open from the start.</div>
				{:else}
					<div class="unlock" class:locked={view.zoneUnlock.locked} data-testid="codex-zone-unlock">
						<span class="unlock-mark"></span>
						<span class="unlock-text">
							{#if view.zoneUnlock.locked}
								Complete a sealed challenge to unlock.
							{:else}
								Complete <strong>{view.zoneUnlock.challengeName}</strong>.
							{/if}
						</span>
						<span class="unlock-state">{view.zoneUnlock.locked ? 'Sealed' : 'Met'}</span>
					</div>
				{/if}
			</section>
		</div>
	</div>
{/if}

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { zoneStatusColor, zoneStatusLabel } from './codex-display';
import ZoneSpawnsPanel from './ZoneSpawnsPanel.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.dossier {
	width: 380px;
	flex: none;
	background: var(--surface);
	border-left: 1px solid var(--border-subtle);
	display: flex;
	flex-direction: column;
}

.head {
	border-left: 3px solid var(--accent);
	padding: 18px 20px 14px;
	flex: none;
}

.kind-line {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 6px;
}

.kind-dot {
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--accent);
	flex: none;
}

.kind {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--accent);
}

.name {
	font-size: 21px;
	font-weight: 500;
	letter-spacing: -0.3px;
	line-height: 1.05;
}

.meta {
	display: flex;
	align-items: center;
	gap: 7px;
	margin-top: 7px;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.status {
	margin-left: auto;
	color: var(--zone-status);
}

.body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding: 16px 20px 20px;
	display: flex;
	flex-direction: column;
	gap: 20px;
}

.block-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 10px;
}

.boss {
	display: flex;
	align-items: center;
	gap: 11px;
	width: 100%;
	background: color-mix(in srgb, var(--boss-accent) 7%, transparent);
	border: 1px solid color-mix(in srgb, var(--boss-accent) 28%, transparent);
	border-radius: 4px;
	padding: 11px 13px;
	cursor: pointer;
	text-align: left;
	color: inherit;

	&:hover {
		border-color: color-mix(in srgb, var(--boss-accent) 55%, transparent);
	}
}

.boss-mark {
	width: 9px;
	height: 9px;
	transform: rotate(45deg);
	background: var(--boss-accent);
	flex: none;
}

.boss-text {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.boss-name {
	font-size: 14px;
	color: var(--text-primary);
}

.boss-tier {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--boss-accent);
}

.chev {
	font-size: 18px;
	color: var(--text-muted);
	flex: none;
}

.empty {
	font-size: 12px;
	color: var(--text-muted);
}

.unlock {
	display: flex;
	align-items: center;
	gap: 9px;
	background: color-mix(in srgb, var(--accent) 6%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 4px;
	padding: 10px 12px;
	font-size: 12px;
	color: var(--text-secondary);

	&.open {
		color: var(--text-muted);
	}
}

.unlock-mark {
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--accent);
	flex: none;
}

.unlock.locked .unlock-mark {
	background: var(--text-muted);
}

.unlock-text {
	flex: 1;
	min-width: 0;

	strong {
		color: var(--text-primary);
		font-weight: 600;
	}
}

.unlock-state {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--accent);
	border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent);
	border-radius: 6px;
	padding: 2px 6px;
	flex: none;
}

.unlock.locked .unlock-state {
	color: var(--text-muted);
	border-color: var(--border-light);
}
</style>
