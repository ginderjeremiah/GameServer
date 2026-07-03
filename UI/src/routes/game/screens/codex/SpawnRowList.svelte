<!-- Shared share-bar row list backing the Spawns sub-tab (enemy → zones) and the zone dossier's spawn
     table (zone → enemies): a heading, then each target's share of the pool as a name/percent row over
     a fill bar, cross-linking into that target's own dossier. -->
<div class="spawns">
	<div class="label">{heading}</div>
	{#if rows.length > 0}
		<div class="list">
			{#each rows as row (row.id)}
				<button type="button" class="spawn" data-testid="{testidPrefix}-{row.id}" onclick={() => onSelect(row.id)}>
					<div class="top">
						<span class="name">{row.name}</span>
						<span class="share">{row.share}%</span>
					</div>
					<div class="bottom">
						<span class="bar-wrap"><Bar value={row.share} presentational /></span>
						<span class="weight">{row.weightLabel}</span>
					</div>
				</button>
			{/each}
		</div>
	{:else if emptyMessage}
		<div class="empty">{emptyMessage}</div>
	{/if}
</div>

<script lang="ts">
import { Bar } from '$components';

export interface SpawnRowVM {
	id: number;
	name: string;
	share: number;
	weightLabel: string;
}

interface Props {
	heading: string;
	rows: SpawnRowVM[];
	/** Shown only when `rows` is empty; omit to render nothing (the enemy panel always has a row). */
	emptyMessage?: string;
	/** Prefix for each row's `data-testid`, e.g. `codex-enemy-spawn` / `codex-zone-spawn`. */
	testidPrefix: string;
	onSelect: (id: number) => void;
}

let { heading, rows, emptyMessage, testidPrefix, onSelect }: Props = $props();
</script>

<style lang="scss">
@use '$styles/codex-dossier' as dossier;

.label {
	@include dossier.section-label;
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

.name {
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

.empty {
	@include dossier.empty-note;
}
</style>
