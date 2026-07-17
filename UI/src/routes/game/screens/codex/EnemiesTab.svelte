<!-- The Enemies tab: a toolbar (search + Normal/Boss filter + sort) over the enemy table on the left,
     and the selected enemy's dossier on the right. -->
<div class="enemies">
	<div class="list-col">
		<div class="toolbar">
			<label class="search">
				<span class="search-icon" aria-hidden="true">⌕</span>
				<input
					class="search-input"
					type="search"
					placeholder="search {view.enemiesTab.enemies.length} enemies"
					aria-label="Search enemies"
					data-testid="codex-search"
					bind:value={view.enemiesTab.search}
				/>
			</label>
			<div class="chips">
				{#each ENEMY_FILTERS as chip (chip.key)}
					<button
						type="button"
						class="chip"
						class:on={view.enemiesTab.filter === chip.key}
						data-testid="codex-filter-{chip.key}"
						onclick={() => view.enemiesTab.setFilter(chip.key)}
					>
						{chip.label}
					</button>
				{/each}
			</div>
			<span class="spacer"></span>
			<span class="count">{view.enemiesTab.shownCount} shown</span>
			<label class="sort">
				<span class="sort-label">sort:</span>
				<select
					class="sort-select"
					aria-label="Sort enemies"
					data-testid="codex-sort"
					bind:value={view.enemiesTab.sort}
				>
					{#each ENEMY_SORTS as option (option.key)}
						<option value={option.key}>{option.label}</option>
					{/each}
				</select>
			</label>
		</div>

		<EnemyTable {view} />
	</div>

	<EnemyDossier {view} />
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';
import { ENEMY_FILTERS, ENEMY_SORTS } from './codex-display';
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
	flex: none;
}

.search-input {
	flex: 1;
	min-width: 0;
	background: transparent;
	border: none;
	outline: none;
	color: var(--text-primary);
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.5px;

	&::placeholder {
		color: var(--text-muted);
	}

	// Hide the native search clear affordance to keep the mono toolbar look consistent.
	&::-webkit-search-cancel-button {
		appearance: none;
	}
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

.sort {
	display: flex;
	align-items: center;
	gap: 5px;
}

.sort-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.sort-select {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-secondary);
	background: transparent;
	border: 1px solid var(--border-light);
	border-radius: 3px;
	padding: 3px 6px;
	cursor: pointer;

	// The dropdown surface is themed; native option chrome stays the system default.
	option {
		background: var(--panel);
		color: var(--text-primary);
	}
}
</style>
