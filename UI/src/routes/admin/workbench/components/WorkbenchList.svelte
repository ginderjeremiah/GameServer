<div class="list-pane" data-testid="workbench-list">
	<div class="list-head">
		<div class="list-title">
			<span class="title-text">{entity.label}</span>
			<span class="meta">{liveCount}</span>
			<div class="spacer"></div>
			<button type="button" class="btn primary sm" data-testid="workbench-new" onclick={onNew}>
				<WorkbenchIcon kind="plus" size={12} />New
			</button>
		</div>
		<div class="search">
			<WorkbenchIcon kind="search" sw={1.4} />
			<input class="inp" placeholder="Search {entity.label.toLowerCase()}…" bind:value={q} />
		</div>
	</div>

	<div class="list-scroll">
		{#each filtered as record (record.id)}
			{@const status = store.status(record)}
			{@const warns = entityWarnings(entity, record)}
			{@const badge = entity.listBadge?.(record)}
			{@const edge =
				status === 'added'
					? 'var(--change-added)'
					: status === 'modified'
						? 'var(--change-modified)'
						: status === 'deleted'
							? 'var(--change-removed)'
							: 'transparent'}
			{@const retired = (entity.retireable ?? false) && store.isRetired(record)}
			<button
				type="button"
				class="list-row"
				data-testid="workbench-row"
				class:selected={record.id === selectedId}
				class:deleted={status === 'deleted'}
				class:retired
				onclick={() => onSelect(record.id)}
			>
				<div
					class="row-edge"
					style:width="3px"
					style:height="34px"
					style:background={edge}
					style:box-shadow={edge === 'transparent' ? 'none' : `0 0 7px ${edge}`}
				></div>
				<div class="row-body">
					<div class="row-top">
						<span class="row-name" class:selected={record.id === selectedId} class:struck={status === 'deleted'}>
							{#if record.name}{record.name}{:else}<span class="blank">{entity.blankName}</span>{/if}
						</span>
						{#if badge}
							<span class="rare-tag" style:color={entity.badgeColor?.(record) ?? 'var(--text-secondary)'}>{badge}</span>
						{/if}
						{#if status === 'added'}<span class="spill added">new</span>{/if}
						{#if status === 'modified'}<span class="spill modified">edited</span>{/if}
						{#if retired}<span class="spill retired">retired</span>{/if}
						<div class="spacer"></div>
						{#if status !== 'deleted' && warns.length > 0}
							<WarnTriangle title={warns.join(' · ')} />
						{/if}
					</div>
					<span class="meta">
						{#each entity.meta(record) as [label, value] (label)}
							<span
								>{#if label}<b>{value}</b> {label}{:else}<b class="bare">{value}</b>{/if}</span
							>
						{/each}
					</span>
				</div>
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import type { EntityConfig, Identified } from '../entities/types';
import type { EntityStore } from '../entity-store.svelte';
import { entityWarnings } from '../validation';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import WarnTriangle from './WarnTriangle.svelte';

interface Props {
	entity: EntityConfig<Identified>;
	store: EntityStore<Identified>;
	selectedId: number;
	onSelect: (id: number) => void;
	onNew: () => void;
}

const { entity, store, selectedId, onSelect, onNew }: Props = $props();

let q = $state('');
const filtered = $derived(store.items.filter((it) => (it.name ?? '').toLowerCase().includes(q.toLowerCase())));
const liveCount = $derived(store.items.filter((it) => store.status(it) !== 'deleted').length);
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
}
.list-title {
	display: flex;
	align-items: baseline;
	gap: 9px;
	margin-bottom: 12px;

	.title-text {
		font-size: 14px;
		font-weight: 500;
	}
}
.spacer {
	flex: 1;
}
.list-scroll {
	flex: 1;
	overflow-y: auto;
	overflow-x: hidden;
}
.list-row {
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
		background: color-mix(in srgb, var(--white) 2%, transparent);
	}
	&.selected {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
	&.deleted {
		opacity: 0.45;
	}
	// Retired records stay in the catalogue (still resolvable by id), just dimmed — distinct
	// from the stronger fade + strikethrough of a pending hard-delete.
	&.retired:not(.deleted) {
		opacity: 0.6;
	}
}
.row-body {
	flex: 1;
	min-width: 0;
}
.row-top {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}
.row-name {
	font-size: 13.5px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	&.selected {
		color: var(--text-primary);
		font-weight: 500;
	}
	&.struck {
		text-decoration: line-through;
	}
	.blank {
		color: var(--text-muted);
		font-style: italic;
	}
}
.meta .bare {
	color: var(--text-tertiary);
}
</style>
