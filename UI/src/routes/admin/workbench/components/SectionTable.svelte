{#if rows.length === 0}
	<EmptySection
		icon={section.emptyIcon}
		title={section.emptyTitle}
		sub={section.emptySub}
		addLabel={section.addLabel}
		onAdd={add}
		{noFree}
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
				{#each rows as row, i (row[rowKey])}
					{@const baseRow = baseByKey[row[rowKey] as number]}
					{@const isNewRow = baseRow === undefined}
					{@const edge = isNewRow
						? 'var(--change-added)'
						: !recordsEqual(row, baseRow)
							? 'var(--change-modified)'
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
								dirty={!isNewRow && !recordsEqual(row[col.key], baseRow[col.key])}
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
		<div class="table-actions">
			<button
				type="button"
				class="btn sm"
				onclick={add}
				disabled={noFree}
				title={noFree ? 'Every option is already assigned' : undefined}
			>
				<WorkbenchIcon kind="plus" size={12} />{section.addLabel}
			</button>
			{#each section.actions ?? [] as action (action.label)}
				<button type="button" class="btn sm" onclick={() => mutateRows(action.apply)}>
					{#if action.glyph}<WorkbenchIcon kind={action.glyph} size={12} />{/if}{action.label}
				</button>
			{/each}
		</div>
	</div>
{/if}

<script lang="ts">
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import type { EntityStore } from '../entity-store.svelte';
import { recordsEqual } from '../entity-store.svelte';
import { fieldsOf, type Identified, type TableRow, type TableSectionConfig } from '../entities/types';
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
const rowKey = $derived(section.rowKey);
const rows = $derived((fieldsOf(record)[itemsKey] as TableRow[]) ?? []);
const baseRows = $derived(baseline ? (fieldsOf(baseline)[itemsKey] as TableRow[]) : null);

// Match each row to its baseline by stable identity (not array position), so a mid-list delete
// shifting indices can no longer compare a row against the wrong baseline.
const baseByKey = $derived.by(() => {
	const map: Record<number, TableRow> = {};
	for (const baseRow of baseRows ?? []) {
		map[baseRow[rowKey] as number] = baseRow;
	}
	return map;
});

// Disable Add once a unique select/attribute column has exhausted every option.
const uniqueCol = $derived(section.columns.find((c) => (c.type === 'select' || c.type === 'attribute') && c.unique));
const noFree = $derived(
	uniqueCol ? (uniqueCol.options?.() ?? []).every((o) => rows.some((r) => r[uniqueCol.key] === o.value)) : false
);

const mutateRows = (mutate: (list: TableRow[]) => void) => {
	store.patch(record.id, (draft) => {
		mutate(fieldsOf(draft)[itemsKey] as TableRow[]);
	});
};

const add = () =>
	store.patch(record.id, (draft) => {
		const list = fieldsOf(draft)[itemsKey] as TableRow[];
		const row = section.newRow(draft);
		// Surrogate-id collections mark new rows with id 0; give each a unique negative id so its
		// identity (and the {#each} key) stays stable and collision-free across several unsaved rows.
		// The persist layer treats any id <= 0 as an Add, normalising it back before sending.
		if (rowKey === 'id') {
			row.id = Math.min(0, ...list.map((r) => (r.id as number) ?? 0)) - 1;
		}
		list.push(row);
	});
const removeRow = (i: number) => mutateRows((list) => list.splice(i, 1));
const setCell = (i: number, key: string, value: number | string | undefined) =>
	mutateRows((list) => {
		// Editing a row's identity column (e.g. a tour step's author-editable `ordinal`) into another
		// row's value would duplicate the {#each} key — crashing dev builds and mis-reconciling prod
		// ones. Swap the two rows' identities instead, so the edit still lands and both keys stay unique.
		if (key === rowKey) {
			const collision = list.find((r, ri) => ri !== i && r[rowKey] === value);
			if (collision) {
				collision[rowKey] = list[i][rowKey];
			}
		}
		list[i][key] = value;
	});
</script>

<style lang="scss">
.table-actions {
	display: flex;
	flex-wrap: wrap;
	gap: 8px;
	margin-top: 12px;
}
</style>
