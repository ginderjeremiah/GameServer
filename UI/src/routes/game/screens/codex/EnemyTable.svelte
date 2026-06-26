<!-- The enemy table: a column header over a scrollable list of selectable enemy rows. Each row shows
     a mark (gold diamond for a boss, neutral dot otherwise), the name, a BOSS tag, the level band and
     the zone / skill counts. -->
<div class="table">
	<div class="head">
		<span class="c-name">Name</span>
		<span class="c-num">Level</span>
		<span class="c-num narrow">Zones</span>
		<span class="c-num narrow">Skills</span>
	</div>

	<div class="rows" data-testid="codex-enemy-rows">
		{#each view.enemyRows as row (row.id)}
			<button
				type="button"
				class="row"
				class:selected={row.id === view.selectedEnemyId}
				class:boss={row.isBoss}
				data-testid="codex-enemy-{row.id}"
				onclick={() => view.selectEnemy(row.id)}
			>
				<span class="c-name name-cell">
					<span class="mark"></span>
					<span class="name">{row.name}</span>
					{#if row.isBoss}
						<span class="boss-tag">BOSS</span>
					{/if}
				</span>
				<span class="c-num band">{row.band}</span>
				<span class="c-num narrow muted">{row.zoneCount}</span>
				<span class="c-num narrow muted">{row.skillCount}</span>
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.table {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
}

.head {
	display: flex;
	align-items: center;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
	padding: 0 14px 8px;
	border-bottom: 1px solid var(--border-subtle);
	margin-right: 14px;
}

.c-name {
	flex: 2.4;
	min-width: 0;
}

.c-num {
	flex: 1;
	text-align: right;
}

.narrow {
	flex: 0.9;
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
	align-items: center;
	padding: 9px 14px;
	border: none;
	border-left: 2px solid transparent;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
	background: transparent;
	cursor: pointer;
	font-family: var(--sans);

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.selected {
		border-left-color: var(--enemy-accent);
		background: color-mix(in srgb, var(--enemy-accent) 10%, transparent);

		.name {
			color: var(--white);
			font-weight: 600;
		}
	}

	&.selected.boss {
		border-left-color: var(--boss-accent);
		background: color-mix(in srgb, var(--boss-accent) 10%, transparent);
	}
}

.name-cell {
	display: flex;
	align-items: center;
	gap: 11px;
	min-width: 0;
}

.mark {
	width: 9px;
	height: 9px;
	flex: none;
	border-radius: 50%;
	background: color-mix(in srgb, var(--white) 28%, transparent);
}

.row.boss .mark {
	border-radius: 0;
	transform: rotate(45deg);
	background: var(--boss-accent);
	box-shadow: 0 0 6px color-mix(in srgb, var(--boss-accent) 50%, transparent);
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

.band {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-secondary);
}

.muted {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);
}
</style>
