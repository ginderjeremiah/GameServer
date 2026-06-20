<!-- The zone dossier's spawn table: the enemies that spawn in the zone with each one's share, ordered
     by share. Rows deep-link into the enemy's dossier on the Enemies tab. -->
<div class="spawns">
	<div class="label">
		{view.zoneSpawnRows.length === 0 ? 'No spawns in this zone' : `Spawn table · ${view.zoneSpawnRows.length}`}
	</div>
	<div class="list">
		{#each view.zoneSpawnRows as spawn (spawn.enemyId)}
			<button
				type="button"
				class="spawn"
				class:boss={spawn.isBoss}
				data-testid="codex-zone-spawn-{spawn.enemyId}"
				onclick={() => view.openEnemy(spawn.enemyId)}
			>
				<div class="top">
					<span class="enemy">
						<span class="enemy-mark"></span>
						<span class="enemy-name">{spawn.enemyName}</span>
						{#if spawn.isBoss}<span class="boss-tag">BOSS</span>{/if}
					</span>
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
	gap: 9px;
}

.spawn {
	display: block;
	width: 100%;
	background: transparent;
	border: none;
	border-left: 2px solid transparent;
	cursor: pointer;
	padding: 4px 4px 4px 9px;
	text-align: left;
	color: inherit;

	&:hover {
		border-left-color: var(--accent);
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.boss:hover {
		border-left-color: var(--boss-accent);
	}
}

.top {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
	margin-bottom: 5px;
}

.enemy {
	display: flex;
	align-items: center;
	gap: 8px;
	min-width: 0;
}

.enemy-mark {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	background: var(--text-muted);
	flex: none;
}

.boss .enemy-mark {
	background: var(--boss-accent);
}

.enemy-name {
	font-size: 12.5px;
	color: var(--text-primary);
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.boss-tag {
	font-family: var(--mono);
	font-size: 7.5px;
	letter-spacing: 1px;
	color: var(--boss-accent);
	border: 1px solid color-mix(in srgb, var(--boss-accent) 45%, transparent);
	border-radius: 6px;
	padding: 1px 5px;
	flex: none;
}

.share {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--accent-light);
	flex: none;
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

.boss .bar-wrap {
	--bar-fill: var(--boss-accent);
}

.weight {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
	white-space: nowrap;
}
</style>
