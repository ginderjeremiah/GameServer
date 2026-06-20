<!-- The Zones rail (left): a vertical progression timeline of every non-retired zone in authored order.
     Each entry carries a status dot (cleared / unlocked / locked), the zone name, its level band and its
     spawn-pool count. Selecting a zone drives the dossier on the right. -->
<div class="rail-col">
	<div class="head">
		<span class="title">Progression</span>
		<span class="count">{view.zoneRows.length} zones</span>
	</div>

	<div class="rail" data-testid="codex-zone-rows">
		{#each view.zoneRows as zone (zone.id)}
			<button
				type="button"
				class="zone"
				class:selected={zone.selected}
				style:--zone-status={zoneStatusColor(zone.status)}
				data-testid="codex-zone-{zone.id}"
				onclick={() => view.selectZone(zone.id)}
			>
				<span class="mark" aria-hidden="true"><span class="dot"></span></span>
				<span class="text">
					<span class="name-row">
						<span class="name">{zone.name}</span>
						<span class="band">{zone.band}</span>
					</span>
					<span class="meta">
						<span class="status">{zoneStatusLabel(zone.status)}</span>
						<span class="sep">·</span>
						<span class="spawns">{zone.spawnCount} {zone.spawnCount === 1 ? 'enemy' : 'enemies'}</span>
					</span>
				</span>
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { zoneStatusColor, zoneStatusLabel } from './codex-display';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.rail-col {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	padding: 14px 0 14px 30px;
}

.head {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 10px;
	margin-bottom: 12px;
	padding-right: 22px;
}

.title {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-secondary);
}

.count {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
	white-space: nowrap;
}

.rail {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding-right: 12px;
	display: flex;
	flex-direction: column;
}

.zone {
	position: relative;
	display: flex;
	align-items: stretch;
	gap: 12px;
	background: transparent;
	border: none;
	cursor: pointer;
	padding: 9px 12px 9px 0;
	text-align: left;
	color: inherit;
}

// The connecting spine: a line behind the status dots linking the rail into a timeline.
.mark {
	position: relative;
	width: 14px;
	flex: none;
	display: flex;
	justify-content: center;
}

.mark::before {
	content: '';
	position: absolute;
	top: 0;
	bottom: 0;
	width: 1px;
	background: var(--border-light);
}

.zone:first-child .mark::before {
	top: 50%;
}

.zone:last-child .mark::before {
	bottom: 50%;
}

.dot {
	position: relative;
	margin-top: 5px;
	width: 9px;
	height: 9px;
	transform: rotate(45deg);
	background: var(--zone-status);
	box-shadow: 0 0 0 3px var(--surface);
	flex: none;
}

.text {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.name-row {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 10px;
}

.name {
	font-size: 13px;
	color: var(--text-secondary);
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.band {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
	white-space: nowrap;
}

.meta {
	display: flex;
	align-items: center;
	gap: 6px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
}

.status {
	color: var(--zone-status);
}

.sep,
.spawns {
	color: var(--text-muted);
}

.zone.selected {
	.name {
		color: var(--text-primary);
		font-weight: 600;
	}

	.dot {
		box-shadow:
			0 0 0 3px var(--surface),
			0 0 6px color-mix(in srgb, var(--zone-status) 70%, transparent);
	}
}
</style>
