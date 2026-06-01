<div>
	<div class="applied-head">
		<span class="lbl applied-label">Applied · {applied.length}</span>
		<div class="eswitch">
			<button type="button" class:active={mode === 'browse'} onclick={() => (mode = 'browse')}>
				<WorkbenchIcon kind="box" size={13} />Browse
			</button>
			<button type="button" class:active={mode === 'search'} onclick={() => (mode = 'search')}>
				<WorkbenchIcon kind="search" size={13} sw={1.5} />Search
			</button>
		</div>
	</div>

	<div class="applied-box">
		{#if applied.length === 0}
			<span class="tag-empty">No tags yet — use {mode === 'search' ? 'search' : 'browse'} below to add some.</span>
		{:else}
			<div class="applied-pills">
				{#each applied as tag (tag.id)}
					<TagPill {tag} isNew={!!baseIds && !baseIds.includes(tag.id)} onRemove={() => remove(tag.id)} />
				{/each}
			</div>
		{/if}
	</div>

	{#if mode === 'browse'}
		<TagBrowse {ids} onToggle={toggle} />
	{:else}
		<TagSearch {ids} onAdd={add} />
	{/if}
</div>

<script lang="ts">
import { reference } from '../reference.svelte';
import type { EntityStore } from '../entity-store.svelte';
import { fieldsOf, type Identified, type TagsSectionConfig } from '../entities/types';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import TagPill from './TagPill.svelte';
import TagBrowse from './TagBrowse.svelte';
import TagSearch from './TagSearch.svelte';

interface Props {
	section: TagsSectionConfig<Identified>;
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { section, record, baseline, store }: Props = $props();

const itemsKey = $derived(section.itemsKey as string);
let mode = $state<'browse' | 'search'>('browse');

const ids = $derived((fieldsOf(record)[itemsKey] as number[]) ?? []);
const baseIds = $derived(baseline ? (fieldsOf(baseline)[itemsKey] as number[]) : null);
const applied = $derived(ids.map((id) => reference.tagById(id)).filter((t) => !!t));

const add = (id: number) => {
	if (!ids.includes(id)) {
		store.patch(record.id, (draft) => {
			(fieldsOf(draft)[itemsKey] as number[]).push(id);
		});
	}
};
const remove = (id: number) => {
	store.patch(record.id, (draft) => {
		fieldsOf(draft)[itemsKey] = ids.filter((k) => k !== id);
	});
};
const toggle = (id: number) => (ids.includes(id) ? remove(id) : add(id));
</script>

<style lang="scss">
.applied-head {
	display: flex;
	justify-content: space-between;
	align-items: center;
	margin-bottom: 12px;
	gap: 16px;
}
.applied-label {
	margin: 0;
}
.applied-pills {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}
</style>
