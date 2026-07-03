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
@use '$styles/codex-table' as table;

.table {
	@include table.frame;
}

.head {
	@include table.head-row;
}

.c-name {
	@include table.col-name;
}

.c-num {
	@include table.col-num;
}

.narrow {
	@include table.col-narrow;
}

.rows {
	@include table.rows-list;
}

.row {
	@include table.row(var(--enemy-accent));

	&.selected.boss {
		border-left-color: var(--boss-accent);
		background: color-mix(in srgb, var(--boss-accent) 10%, transparent);
	}
}

.name-cell {
	@include table.name-cell;
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
	@include table.row-name;
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
	@include table.muted-num;
}
</style>
