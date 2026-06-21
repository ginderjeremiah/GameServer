<div>
	{#if ids.length === 0}
		<EmptySection icon={section.emptyIcon} title={section.emptyTitle} sub={section.emptySub} />
	{:else}
		<div class="chips">
			{#each ids as id (id)}
				{@const entry = catalogue.find((c) => c.id === id)}
				<div class="skill-chip" class:added={baseIds && !baseIds.includes(id)} class:retired={entry?.retired}>
					<span class="nm">{entry ? section.labelOf(entry) : `#${id}`}</span>
					<span class="dm">{entry ? section.metaOf(entry) : ''}</span>
					{#if entry?.retired}<span class="retired-tag">retired</span>{/if}
					<button
						type="button"
						class="x"
						title="Remove"
						aria-label={`Remove ${entry ? section.labelOf(entry) : `#${id}`}`}
						onclick={() => remove(id)}
					>
						<WorkbenchIcon kind="x" size={11} />
					</button>
				</div>
			{/each}
		</div>
	{/if}

	{#if available.length > 0}
		<div class="fld add-select">
			<select
				class="sel"
				value=""
				onchange={(e) => {
					const { value } = e.currentTarget;
					// The placeholder's empty value coerces to 0 — the same as the zero-based id of
					// the first catalogue entry — so guard on the raw string before adding.
					if (value !== '') {
						add(+value);
					}
				}}
			>
				<option value="">＋ {section.addLabel}</option>
				{#each available as entry (entry.id)}
					<option value={entry.id}>{section.labelOf(entry)} · {section.metaOf(entry)}</option>
				{/each}
			</select>
			<SelectCaret />
		</div>
	{/if}
</div>

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import type { EntityStore } from '../entity-store.svelte';
import { fieldsOf, type ChipsSectionConfig, type Identified } from '../entities/types';
import EmptySection from './EmptySection.svelte';
import SelectCaret from './SelectCaret.svelte';

interface Props {
	section: ChipsSectionConfig<Identified>;
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { section, record, baseline, store }: Props = $props();

const itemsKey = $derived(section.itemsKey as string);
const ids = $derived((fieldsOf(record)[itemsKey] as number[]) ?? []);
const baseIds = $derived(baseline ? (fieldsOf(baseline)[itemsKey] as number[]) : null);
const catalogue = $derived(section.catalogue());
// Retired or non-addable entries (e.g. a skill not flagged for this channel) stay in the
// catalogue so an already-assigned chip still renders its name, but they are excluded from the
// add-list so they can't be newly assigned.
const available = $derived(catalogue.filter((c) => !ids.includes(c.id) && !c.retired && c.addable !== false));

const add = (id: number) => {
	// No truthiness guard on `id`: ids are zero-based, so id 0 is a valid first entry.
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
</script>

<style lang="scss">
.chips {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
}
.skill-chip.retired .nm {
	color: var(--text-muted);
	text-decoration: line-through;
}
.retired-tag {
	font-family: var(--mono);
	font-size: 9.5px;
	text-transform: uppercase;
	letter-spacing: 0.05em;
	color: var(--text-muted);
	border: 1px solid color-mix(in srgb, var(--text-muted) 45%, transparent);
	border-radius: 3px;
	padding: 1px 5px;
}
.add-select {
	margin-top: 14px;
	max-width: 280px;
}
</style>
