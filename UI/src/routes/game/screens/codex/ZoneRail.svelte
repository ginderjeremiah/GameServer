<!-- The zone progression rail: a vertical timeline of zones (authored order), each a selectable row
     with a status node (cleared / unlocked / locked), the zone name + level band, the spawn-pool size
     and a BOSS tag when the zone has a dedicated boss. -->
<div class="rail">
	<div class="rail-head">Progression · {view.zonesTab.zoneRows.length}</div>

	<div class="rows" data-testid="codex-zone-rows">
		{#each view.zonesTab.zoneRows as row (row.id)}
			<button
				type="button"
				class="row"
				class:selected={row.id === view.zonesTab.selectedZoneId}
				style:--status-color={zoneStatusColor(row.status)}
				data-testid="codex-zone-{row.id}"
				onclick={() => view.zonesTab.selectZone(row.id)}
			>
				<span class="node">
					<span class="dot" class:cleared={row.status === 'cleared'} class:locked={row.status === 'locked'}></span>
				</span>
				<span class="info">
					<span class="name-line">
						<span class="name">{row.name}</span>
						{#if row.hasBoss}
							<span class="boss-tag">BOSS</span>
						{/if}
					</span>
					<span class="meta">{row.band} · {row.spawnCount} {row.spawnCount === 1 ? 'spawn' : 'spawns'}</span>
				</span>
				<span class="status">{ZONE_STATUS_LABELS[row.status]}</span>
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { ZONE_STATUS_LABELS, zoneStatusColor } from './codex-display';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.rail {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
}

.rail-head {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
	padding: 0 14px 8px;
	border-bottom: 1px solid var(--border-subtle);
	margin-right: 14px;
}

.rows {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	padding-right: 6px;
}

.row {
	width: 100%;
	text-align: left;
	display: flex;
	align-items: stretch;
	gap: 12px;
	padding: 13px 14px;
	border: none;
	border-left: 2px solid transparent;
	background: transparent;
	cursor: pointer;
	font-family: var(--sans);

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.selected {
		border-left-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 10%, transparent);

		.name {
			color: var(--white);
			font-weight: 600;
		}
	}
}

// The timeline spine: each node draws the line segment above and below its dot, hidden at the ends so
// the rail reads as one continuous connector through the zones.
.node {
	position: relative;
	width: 14px;
	flex: none;
	display: flex;
	align-items: center;
	justify-content: center;
}

.node::before,
.node::after {
	content: '';
	position: absolute;
	left: 50%;
	transform: translateX(-50%);
	width: 1.5px;
	background: var(--border-light);
}

.node::before {
	top: 0;
	bottom: 50%;
}

.node::after {
	top: 50%;
	bottom: 0;
}

.row:first-child .node::before,
.row:last-child .node::after {
	display: none;
}

.dot {
	position: relative;
	width: 11px;
	height: 11px;
	border-radius: 50%;
	background: var(--status-color);
	box-shadow: 0 0 7px color-mix(in srgb, var(--status-color) 45%, transparent);

	&.cleared {
		box-shadow: 0 0 9px color-mix(in srgb, var(--status-color) 65%, transparent);
	}

	// A sealed zone reads as a hollow muted ring rather than a filled node.
	&.locked {
		background: transparent;
		border: 1.5px solid var(--status-color);
		box-shadow: none;
	}
}

.info {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	justify-content: center;
	gap: 3px;
}

.name-line {
	display: flex;
	align-items: center;
	gap: 9px;
	min-width: 0;
}

.name {
	font-size: 13.5px;
	color: var(--text-primary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.boss-tag {
	font-family: var(--mono);
	font-size: 7.5px;
	letter-spacing: 1px;
	color: var(--boss-accent);
	border: 1px solid color-mix(in srgb, var(--boss-accent) 40%, transparent);
	border-radius: 3px;
	padding: 1px 4px;
	flex: none;
}

.meta {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-tertiary);
}

.status {
	align-self: center;
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--status-color);
	white-space: nowrap;
	flex: none;
}
</style>
