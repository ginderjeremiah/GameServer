<div class="workbench">
	{#if store}
		<div class="page-head">
			<div class="eyebrow">Admin Console{groupLabel ? ` · ${groupLabel}` : ''}</div>
			<div class="page-title-row">
				<h1 class="page-title" data-testid="workbench-title">{entity.label}</h1>
				<div class="page-summary">
					<span>{liveItems.length} {entity.label.toLowerCase()}</span>
					{#if flagged > 0}
						<span class="flag"
							><WorkbenchIcon kind="warn" size={12} sw={1.6} stroke="var(--warning)" />{flagged} need attention</span
						>
					{/if}
					{#if store.counts.total > 0}<span class="unsaved">{store.counts.total} unsaved</span>{/if}
				</div>
			</div>
		</div>

		<div class="workbench-body">
			<WorkbenchList {entity} {store} selectedId={selId} onSelect={(id) => (selId = id)} onNew={newItem} />
			<WorkbenchDetail
				{entity}
				{store}
				record={selected}
				baseline={selectedBaseline}
				{tab}
				onTab={(key) => (selectedTab = key)}
				onNew={newItem}
			/>
		</div>
	{:else if error}
		<div class="workbench-error" role="alert" data-testid="workbench-error">
			<p>{error}</p>
			<button type="button" class="btn" onclick={loadSeed}>Refresh</button>
		</div>
	{:else}
		<Loading loading={true} delay={150} />
	{/if}
</div>

<script lang="ts">
import { onMount, onDestroy } from 'svelte';
import { Loading } from '$components';
import { toastError } from '$stores';
import './workbench.scss';
import type { EntityConfig, Identified } from './entities/types';
import { workbenchDirty } from './dirty.svelte';
import { EntityStore } from './entity-store.svelte';
import WorkbenchIcon from './WorkbenchIcon.svelte';
import WorkbenchList from './components/WorkbenchList.svelte';
import WorkbenchDetail from './components/WorkbenchDetail.svelte';

interface Props {
	entity: EntityConfig<Identified>;
	groupLabel?: string;
}

const { entity, groupLabel = '' }: Props = $props();

let store = $state<EntityStore<Identified>>();
let error = $state<string | null>(null);
let selId = $state(0);
let selectedTab = $state<string>();
const tab = $derived(selectedTab ?? entity.sections[0]?.key);

async function loadSeed() {
	error = null;
	try {
		const seed = await entity.refresh();
		store = new EntityStore(entity, seed);
		selId = seed[0]?.id ?? 0;
	} catch (ex) {
		const message = ex instanceof Error ? ex.message : 'Failed to load workbench data.';
		error = message;
		toastError(message);
	}
}

onMount(() => {
	void loadSeed();
});

// Cancel any pending "saved" flash timer so a save that lands near unmount can't
// write into a torn-down store.
onDestroy(() => {
	store?.dispose();
	workbenchDirty.set(0);
});

// Surface this store's pending-change count to the admin shell's tool-switch/unload guard.
$effect(() => {
	workbenchDirty.set(store?.counts.total ?? 0);
});

const selected = $derived(store ? (store.items.find((it) => it.id === selId) ?? store.items[0]) : undefined);
const selectedBaseline = $derived(store && selected ? store.baselineOf(selected.id) : undefined);
const liveItems = $derived.by(() => {
	const s = store;
	return s ? s.items.filter((it) => s.stateOf(it).status !== 'deleted') : [];
});
const flagged = $derived.by(() => {
	const s = store;
	return s ? liveItems.filter((it) => s.stateOf(it).warnings.length > 0).length : 0;
});

// After a save, a selected-but-unsaved record's temporary negative id no longer matches any
// item — follow it to its persisted id first, and only fall back to the first record (e.g.
// the selection was actually deleted then saved) when no remap exists for it.
$effect(() => {
	if (!store) {
		return;
	}
	const persistedId = store.lastIdMap.get(selId);
	if (persistedId !== undefined) {
		selId = persistedId;
	} else if (selected && selected.id !== selId) {
		selId = selected.id;
	}
});

const newItem = () => {
	if (!store) {
		return;
	}

	selId = store.addItem();
	selectedTab = entity.sections[0].key;
};
</script>

<style lang="scss">
.workbench-body {
	display: flex;
	flex: 1;
	min-height: 0;
}
.unsaved {
	color: var(--text-secondary);
}
.workbench-error {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 14px;
	padding: 20px;
	text-align: center;

	p {
		max-width: 480px;
		color: var(--error);
		font-size: 13px;
	}
}
</style>
