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
	{:else}
		<Loading loading={true} delay={150} />
	{/if}
</div>

<script lang="ts">
import { onMount, onDestroy } from 'svelte';
import { Loading } from '$components';
import './workbench.scss';
import type { EntityConfig, Identified } from './entities/types';
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
let selId = $state(0);
let selectedTab = $state<string>();
const tab = $derived(selectedTab ?? entity.sections[0]?.key);

onMount(async () => {
	const seed = await entity.refresh();
	store = new EntityStore(entity, seed);
	selId = seed[0]?.id ?? 0;
});

// Cancel any pending "saved" flash timer so a save that lands near unmount can't
// write into a torn-down store.
onDestroy(() => store?.dispose());

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

// If the selected record was removed (e.g. deleted then saved), fall back to the first.
$effect(() => {
	if (store && selected && selected.id !== selId) {
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
</style>
