{#if rows.length === 0}
	<EmptySection
		icon={section.emptyIcon}
		title={section.emptyTitle}
		sub={section.emptySub}
		addLabel={section.addLabel}
		onAdd={add}
	/>
{:else}
	<div>
		<table class="mtable">
			<thead>
				<tr>
					<th style:width="18px" style:padding="0"></th>
					{#each section.columns as col (col.key)}
						<th class:r={col.align === 'r'}>{col.label}</th>
					{/each}
					<th class="c" style:width="40px"></th>
				</tr>
			</thead>
			<tbody>
				{#each rows as row, i (i)}
					{@const isNewRow = !baseRows || i >= baseRows.length}
					{@const baseRow = baseRows?.[i]}
					{@const edge = isNewRow
						? 'var(--accent)'
						: baseRow && !recordsEqual(row, baseRow)
							? 'var(--warning)'
							: 'transparent'}
					<tr>
						<td style:padding="0" style:width="18px">
							<div
								class="row-edge"
								style:height="28px"
								style:background={edge}
								style:box-shadow={edge === 'transparent' ? 'none' : `0 0 7px ${edge}`}
							></div>
						</td>
						{#each section.columns as col (col.key)}
							<TableCell
								{col}
								{row}
								idx={i}
								{rows}
								{record}
								dirty={!isNewRow && !!baseRow && !recordsEqual(row[col.key], baseRow[col.key])}
								onChange={(value) => setCell(i, col.key, value)}
							/>
						{/each}
						<td class="c">
							<button type="button" class="row-x" title="Remove" onclick={() => removeRow(i)}>
								<WorkbenchIcon kind="x" size={11} />
							</button>
						</td>
					</tr>
				{/each}
			</tbody>
		</table>
		<button
			type="button"
			class="btn sm add-row"
			onclick={add}
			disabled={noFree}
			title={noFree ? 'Every option is already assigned' : undefined}
		>
			<WorkbenchIcon kind="plus" size={12} />{section.addLabel}
		</button>
	</div>
{/if}

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import type { EntityStore } from '../entity-store.svelte';
import { recordsEqual } from '../entity-store.svelte';
import { fieldsOf, type Identified, type TableSectionConfig } from '../entities/types';
import EmptySection from './EmptySection.svelte';
import TableCell from './TableCell.svelte';

interface Props {
	section: TableSectionConfig<Identified>;
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { section, record, baseline, store }: Props = $props();

const itemsKey = $derived(section.itemsKey as string);
const rows = $derived((fieldsOf(record)[itemsKey] as Record<string, number>[]) ?? []);
const baseRows = $derived(baseline ? (fieldsOf(baseline)[itemsKey] as Record<string, number>[]) : null);

// Disable Add once a unique select column has exhausted every option.
const uniqueCol = $derived(section.columns.find((c) => c.type === 'select' && c.unique));
const noFree = $derived(
	uniqueCol ? (uniqueCol.options?.() ?? []).every((o) => rows.some((r) => r[uniqueCol.key] === o.value)) : false
);

const mutateRows = (mutate: (list: Record<string, number>[]) => void) => {
	store.patch(record.id, (draft) => {
		mutate(fieldsOf(draft)[itemsKey] as Record<string, number>[]);
	});
};

const add = () =>
	store.patch(record.id, (draft) => {
		(fieldsOf(draft)[itemsKey] as Record<string, number>[]).push(section.newRow(draft));
	});
const removeRow = (i: number) => mutateRows((list) => list.splice(i, 1));
const setCell = (i: number, key: string, value: number) => mutateRows((list) => (list[i][key] = value));
</script>

<style lang="scss">
.add-row {
	margin-top: 12px;
}
</style>
