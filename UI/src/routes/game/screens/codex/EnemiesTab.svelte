<!-- The Enemies tab: a toolbar (search + Normal/Boss filter) over the enemy table on the left, and
     the selected enemy's dossier on the right. Search and sort are display-only in this slice. -->
<div class="enemies">
	<div class="list-col">
		<div class="toolbar">
			<div class="search" aria-hidden="true">
				<span class="search-icon">⌕</span>
				<span class="search-text">search {view.enemies.length} enemies</span>
			</div>
			<div class="chips">
				{#each ENEMY_FILTERS as chip (chip.key)}
					<button
						type="button"
						class="chip"
						class:on={view.filter === chip.key}
						data-testid="codex-filter-{chip.key}"
						onclick={() => view.setFilter(chip.key)}
					>
						{chip.label}
					</button>
				{/each}
			</div>
			<span class="spacer"></span>
			<span class="count">{view.shownCount} shown · sort: level ▾</span>
		</div>

		<EnemyTable {view} />
	</div>

	<EnemyDossier {view} />
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { ENEMY_FILTERS } from './codex-display';
import EnemyTable from './EnemyTable.svelte';
import EnemyDossier from './EnemyDossier.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.enemies {
	flex: 1;
	min-height: 0;
	display: flex;
}

.list-col {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	padding: 14px 0 14px 30px;
}

.toolbar {
	display: flex;
	align-items: center;
	gap: 10px;
	margin-bottom: 12px;
	padding-right: 22px;
}

.search {
	display: flex;
	align-items: center;
	gap: 8px;
	background: var(--panel);
	border: 1px solid var(--border-light);
	border-radius: 3px;
	padding: 6px 11px;
	width: 190px;
}

.search-icon {
	color: var(--text-muted);
	font-size: 12px;
}

.search-text {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-muted);
	letter-spacing: 0.5px;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.chips {
	display: flex;
	gap: 5px;
}

.chip {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	background: transparent;
	color: var(--text-tertiary);
	border: 1px solid var(--border-light);
	border-radius: 10px;
	padding: 4px 11px;
	cursor: pointer;

	&.on {
		background: var(--text-primary);
		color: var(--text-on-accent);
		border-color: var(--text-primary);
	}
}

.spacer {
	flex: 1;
}

.count {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
	white-space: nowrap;
}
</style>
