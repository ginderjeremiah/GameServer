<!-- Spawns sub-tab: the zones this enemy spawns in (with its share of each zone's spawn table), or a
     single "Encounter" row for a boss. Each row cross-links into that zone's dossier. -->
<div class="spawns">
	<div class="label">{view.spawnHeading}</div>
	<div class="list">
		{#each view.spawns as spawn (spawn.zoneId)}
			<button
				type="button"
				class="spawn"
				data-testid="codex-enemy-spawn-{spawn.zoneId}"
				onclick={() => view.openZone(spawn.zoneId)}
			>
				<div class="top">
					<span class="zone">{spawn.zoneName}</span>
					<span class="share">{spawn.share}%</span>
				</div>
				<div class="bottom">
					<span class="bar-wrap"><Bar value={spawn.share} presentational /></span>
					<span class="weight">{spawn.weightLabel}</span>
				</div>
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import { Bar } from '$components';
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 10px;
}

.list {
	display: flex;
	flex-direction: column;
	gap: 11px;
}

.spawn {
	display: block;
	width: 100%;
	text-align: left;
	background: transparent;
	border: none;
	border-radius: 4px;
	padding: 4px;
	margin: -4px;
	cursor: pointer;
	font-family: var(--sans);

	&:hover {
		background: color-mix(in srgb, var(--white) 4%, transparent);
	}
}

.top {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-bottom: 5px;
}

.zone {
	font-size: 12.5px;
	color: var(--text-primary);
}

.share {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--accent-light);
}

.bottom {
	display: flex;
	align-items: center;
	gap: 8px;
}

.bar-wrap {
	flex: 1;
	--bar-height: 6px;
	--bar-radius: 3px;
	--bar-fill: var(--accent);
	--bar-track-bg: color-mix(in srgb, var(--white) 6%, transparent);
}

.weight {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
	white-space: nowrap;
}
</style>
