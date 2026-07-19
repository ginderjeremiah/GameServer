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
			<input
				class="inp"
				aria-label="Search {entity.label.toLowerCase()}"
				placeholder="Search {entity.label.toLowerCase()}…"
				bind:value={q}
			/>
		</div>
	</div>

	<div class="list-scroll">
		{#each filtered as record (record.id)}
			{@const state = store.stateOf(record)}
			{@const retired = (entity.retireable ?? false) && store.isRetired(record)}
			<ListRow
				testId="workbench-row"
				selected={record.id === selectedId}
				status={state.status}
				{retired}
				name={displayName(record)}
				blank={entity.blankName}
				badge={entity.listBadge?.(record)}
				badgeColor={entity.badgeColor?.(record)}
				warnings={state.warnings}
				onSelect={() => onSelect(record.id)}
			>
				{#snippet meta()}
					{#each entity.meta(record) as [label, value] (label)}
						<span
							>{#if label}<b>{value}</b> {label}{:else}<b class="bare">{value}</b>{/if}</span
						>
					{/each}
				{/snippet}
			</ListRow>
		{/each}
	</div>
</div>

<script lang="ts">
import type { EntityConfig, Identified } from '../entities/types';
import type { EntityStore } from '../entity-store.svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import ListRow from './ListRow.svelte';

interface Props {
	entity: EntityConfig<Identified>;
	store: EntityStore<Identified>;
	selectedId: number;
	onSelect: (id: number) => void;
	onNew: () => void;
}

const { entity, store, selectedId, onSelect, onNew }: Props = $props();

/** The row label: the entity's live `title` hook (for a nameless entity) falling back to the stored name. */
const displayName = (record: Identified): string => entity.title?.(record) || record.name || '';

let q = $state('');
const filtered = $derived(store.items.filter((it) => displayName(it).toLowerCase().includes(q.toLowerCase())));
const liveCount = $derived(store.items.filter((it) => store.stateOf(it).status !== 'deleted').length);
</script>

<style lang="scss">
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
// `.meta`'s wrapper is rendered by ListRow, so scope directly off `.bare` rather than an
// ancestor combinator that Svelte's per-component CSS scoping can't see across.
.bare {
	color: var(--text-tertiary);
}
</style>
