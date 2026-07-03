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
			<span class="search-ic"><WorkbenchIcon kind="search" size={13} /></span>
			<input class="search-inp" placeholder="Search…" bind:value={query} aria-label="Search" />
		</div>
	</div>

	<div class="list-scroll">
		{#each rows as row (row.id)}
			<button
				type="button"
				class="row"
				class:selected={row.id === selectedId}
				onclick={() => (drilled ? store.drillTier(row.id) : store.selectPath(row.id))}
			>
				<span class="edge" class:added={row.status === 'added'} class:modified={row.status === 'modified'}></span>
				{#if drilled}
					<span class="ord" class:sel={row.id === selectedId}>{row.ordinal}</span>
				{/if}
				<span class="row-main">
					<span class="row-name-line">
						<span class="row-name" class:blank={!row.name}>{row.name || row.blank}</span>
						{#if row.status === 'added'}<span class="spill added">new</span>{/if}
						{#if row.status === 'modified'}<span class="spill modified">edited</span>{/if}
						{#if row.retired}<span class="spill retired">retired</span>{/if}
						{#if row.warn}<span class="spacer"></span><WorkbenchIcon
								kind="warn"
								size={11}
								stroke="var(--warning)"
							/>{/if}
					</span>
					<span class="row-meta">{row.meta}</span>
				</span>
			</button>
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
	status: 'added' | 'modified' | 'clean';
	retired: boolean;
	warn: boolean;
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
				warn: proficiencyWarnings(t).length > 0,
				ordinal: t.pathOrdinal
			}));
	}
	return store.paths
		.filter((p) => matches(p.name))
		.map((p) => {
			const tiers = tiersOfPath(store.profs, p.id);
			return {
				id: p.id,
				name: p.name,
				blank: 'Unnamed path',
				meta: `${tiers.length} ${tiers.length === 1 ? 'tier' : 'tiers'} · ${activityKeyLabel(p.activityKey)}`,
				status: store.pathStatus(p),
				retired: store.isRetired(p),
				warn: pathWarnings(p).length > 0 || hasTierCollision(tiers),
				ordinal: 0
			};
		});
});
</script>

<style lang="scss">
.list-pane {
	width: 322px;
	flex-shrink: 0;
	border-right: 1px solid var(--border-subtle);
	display: flex;
	flex-direction: column;
	background: var(--surface);
}
.list-head {
	padding: 16px 16px 12px;
	border-bottom: 1px solid var(--border-subtle);
	flex-shrink: 0;
}
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
.spacer {
	flex: 1;
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
.search {
	position: relative;
}
.search-ic {
	position: absolute;
	left: 10px;
	top: 50%;
	transform: translateY(-50%);
	color: var(--text-muted);
	display: flex;
	pointer-events: none;
}
.search-inp {
	width: 100%;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-size: 13px;
	padding: 8px 11px 8px 31px;
}
.list-scroll {
	flex: 1;
	overflow-y: auto;
}
.row {
	width: 100%;
	text-align: left;
	background: transparent;
	border: none;
	border-bottom: 1px solid var(--border-subtle);
	padding: 10px 14px 10px 0;
	cursor: pointer;
	display: flex;
	align-items: center;
	gap: 10px;

	&:hover {
		background: color-mix(in srgb, var(--white) 4%, transparent);
	}
	&.selected {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
}
.edge {
	width: 3px;
	height: 34px;
	flex-shrink: 0;
	background: transparent;

	&.added {
		background: var(--change-added);
		box-shadow: 0 0 7px var(--change-added);
	}
	&.modified {
		background: var(--change-modified);
		box-shadow: 0 0 7px var(--change-modified);
	}
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
.row-main {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 4px;
}
.row-name-line {
	display: flex;
	align-items: center;
	gap: 8px;
}
.row-name {
	font-size: 13.5px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	&.blank {
		color: var(--text-muted);
	}
}
.row.selected .row-name {
	color: var(--text-primary);
	font-weight: 500;
}
.row-meta {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-tertiary);
}
.spill {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	padding: 2px 6px;
	border-radius: 3px;

	&.added {
		color: var(--change-added);
		border: 1px solid color-mix(in srgb, var(--change-added) 40%, transparent);
		background: color-mix(in srgb, var(--change-added) 10%, transparent);
	}
	&.modified {
		color: var(--change-modified);
		border: 1px solid color-mix(in srgb, var(--change-modified) 40%, transparent);
		background: color-mix(in srgb, var(--change-modified) 10%, transparent);
	}
	&.retired {
		color: var(--text-muted);
		border: 1px solid var(--border-light);
	}
}
.list-empty {
	padding: 24px 16px;
	color: var(--text-muted);
	font-size: 12.5px;
	text-align: center;
}
</style>
