<div class="list-pane" data-testid="progression-list">
	<div class="list-head">
		{#if drilled}
			<button type="button" class="back" onclick={() => store.back()}>
				<WorkbenchIcon kind="back" size={11} />{store.selectedPath?.name || 'Path'}
			</button>
		{/if}
		<div class="list-title-row">
			<span class="list-title">{drilled ? 'Tiers' : 'Paths'}</span>
			<span class="list-count">{rows.length}</span>
			<div class="spacer"></div>
			{#if !drilled}
				<button type="button" class="new-btn" data-testid="progression-new-path" onclick={() => store.addPath()}>
					<WorkbenchIcon kind="plus" size={11} />New
				</button>
			{/if}
		</div>
		<div class="search">
			<WorkbenchIcon kind="search" sw={1.4} />
			<input class="inp" placeholder="Search…" bind:value={query} aria-label="Search" />
		</div>
	</div>

	<div class="list-scroll">
		{#each rows as row (row.id)}
			<ListRow
				testId="progression-row"
				selected={row.id === selectedId}
				status={row.status}
				retired={row.retired}
				name={row.name}
				blank={row.blank}
				warnings={row.warnings}
				onSelect={() => (drilled ? store.drillTier(row.id) : store.selectPath(row.id))}
			>
				{#snippet leading()}
					{#if drilled}
						<span class="ord" class:sel={row.id === selectedId}>{row.ordinal}</span>
					{/if}
				{/snippet}
				{#snippet meta()}
					{row.meta}
				{/snippet}
			</ListRow>
		{/each}
		{#if rows.length === 0}
			<div class="list-empty">{query ? 'No matches' : drilled ? 'No tiers yet' : 'No paths yet'}</div>
		{/if}
	</div>
</div>

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import { activityKeyLabel } from '$lib/common';
import type { ProgressionStore } from './progression-store.svelte';
import { hasTierCollision, pathWarnings, proficiencyWarnings, tiersOfPath } from './progression-helpers';
import ListRow, { type ListRowStatus } from '../components/ListRow.svelte';

interface Props {
	store: ProgressionStore;
}

const { store }: Props = $props();

let query = $state('');

const drilled = $derived(store.drilledTier !== undefined);
const selectedId = $derived(drilled ? store.drilledTierId : store.selectedPathId);

interface Row {
	id: number;
	name: string;
	blank: string;
	meta: string;
	status: ListRowStatus;
	retired: boolean;
	warnings: string[];
	ordinal: number;
}

const matches = (name: string) => name.toLowerCase().includes(query.trim().toLowerCase());

const rows = $derived.by<Row[]>(() => {
	if (drilled) {
		return store.currentTiers
			.filter((t) => matches(t.name))
			.map((t) => ({
				id: t.id,
				name: t.name,
				blank: 'Unnamed tier',
				meta: `cap ${t.maxLevel} · ${t.levelRewards.length} ${t.levelRewards.length === 1 ? 'milestone' : 'milestones'}`,
				status: store.profStatus(t),
				retired: store.isRetired(t),
				warnings: proficiencyWarnings(t),
				ordinal: t.pathOrdinal
			}));
	}
	return store.paths
		.filter((p) => matches(p.name))
		.map((p) => {
			const tiers = tiersOfPath(store.profs, p.id);
			const warnings = pathWarnings(p);
			return {
				id: p.id,
				name: p.name,
				blank: 'Unnamed path',
				meta: `${tiers.length} ${tiers.length === 1 ? 'tier' : 'tiers'} · ${activityKeyLabel(p.activityKey)}`,
				status: store.pathStatus(p),
				retired: store.isRetired(p),
				warnings: hasTierCollision(tiers) ? [...warnings, 'Tiers have colliding order'] : warnings,
				ordinal: 0
			};
		});
});
</script>

<style lang="scss">
.back {
	display: inline-flex;
	align-items: center;
	gap: 7px;
	background: transparent;
	border: none;
	color: var(--accent);
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 0;
	cursor: pointer;
	margin-bottom: 12px;
}
.list-title-row {
	display: flex;
	align-items: baseline;
	gap: 9px;
	margin-bottom: 12px;
}
.list-title {
	font-size: 14px;
	font-weight: 500;
}
.list-count {
	font-family: var(--mono);
	font-size: 11.5px;
	color: var(--text-tertiary);
}
.new-btn {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	background: color-mix(in srgb, var(--accent) 12%, transparent);
	border: 1px solid var(--accent);
	color: var(--accent-light);
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 5px 11px;
	border-radius: 3px;
	cursor: pointer;
}
.ord {
	width: 24px;
	height: 24px;
	flex-shrink: 0;
	border-radius: 50%;
	background: var(--panel-2);
	border: 1px solid var(--border-light);
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 10px;
	display: flex;
	align-items: center;
	justify-content: center;

	&.sel {
		background: color-mix(in srgb, var(--accent) 22%, transparent);
		border-color: var(--accent);
		color: var(--accent-light);
	}
}
.list-empty {
	padding: 24px 16px;
	color: var(--text-muted);
	font-size: 12.5px;
	text-align: center;
}
</style>
