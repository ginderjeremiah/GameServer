<div class="table-editor-toolbar round-border">
	<div class="add-row-button-container">
		<Button text="Add Row" onClick={addNewRow} />
	</div>
</div>
<div class="table-editor-container round-border">
	<table>
		<thead>
			<tr>
				{#if !primaryKey}
					<th>Index</th>
				{/if}
				{#each columns as column}
					<th>{column.name}</th>
				{/each}
				<th>Delete</th>
			</tr>
		</thead>
		<tbody>
			{#each visibleRows as row (row.index)}
				<TableRow
					bind:data={row.data}
					bind:state={row.state}
					index={row.index}
					originalData={row.originalData}
					{columns}
					{options}
				/>
			{/each}
		</tbody>
	</table>
</div>

<script lang="ts" generics="T extends {}">
import { keys, normalizeText } from '$lib/common';
import { Button } from '$components';
import { type EditorOptions, type RowData, RowState } from './types';
import TableRow from './TableRow.svelte';
import { EChangeType, type IChange } from '$lib/api';

export const getChanges = getRowChanges;

const {
	data,
	primaryKey,
	hiddenColumns,
	disabledColumns,
	selectOptions,
	sampleItem
}: EditorOptions<T> = $props();

const options = {
	data,
	primaryKey,
	hiddenColumns,
	disabledColumns,
	selectOptions,
	sampleItem
};

let rows = $state<RowData<T>[]>([]);
const visibleRows = $derived(
	rows.filter((r) => r.state !== RowState.Deleted && r.state !== RowState.AddedDeleted)
);

const sample = $derived(sampleItem ?? rows[0]?.data);
const columns = $derived(
	keys(sample)
		.filter((c) => !hiddenColumns?.some((h) => h === c))
		.map((c) => ({
			key: c,
			name: getKeyName(c),
			disabled: disabledColumns?.some((d) => d === c)
		}))
);

const getKeyName = (key: string | number | symbol) => {
	let keyName = key.toString();
	if (keyName.endsWith('Id') && keyName !== primaryKey) {
		keyName = keyName.slice(0, -2);
	}

	return normalizeText(keyName);
};

let newItemIndex = -1;
const addNewRow = () => {
	const newData = Object.assign({}, sample);
	if (primaryKey) {
		newData[primaryKey] = newItemIndex as any;
	}

	rows.push({
		originalData: sample,
		data: newData,
		index: newItemIndex,
		state: RowState.Added
	});

	newItemIndex--;
};

function getRowChanges() {
	const changes: IChange<T>[] = [];
	for (const row of rows) {
		switch (row.state) {
			case RowState.Added:
				changes.push({
					changeType: EChangeType.Add,
					item: row.data
				});
				break;
			case RowState.Deleted:
				changes.push({
					changeType: EChangeType.Delete,
					item: row.data
				});
				break;
			case RowState.Modified:
				changes.push({
					changeType: EChangeType.Edit,
					item: row.data
				});
				break;
			case RowState.Unmodified:
			case RowState.AddedDeleted:
			//Do nothing...
		}
	}
	return changes;
}

$effect(() => {
	const validData = data.length > 0 && !data[0] ? data.splice(1) : data;
	rows = validData.map((d, i) => {
		const data = $state(Object.assign({}, d));
		let state = $state(RowState.Unmodified);
		return {
			originalData: d,
			data,
			index: i,
			get state() {
				return state;
			},
			set state(value) {
				state = value;
			}
		};
	});
});
</script>

<style lang="scss">
.table-editor-toolbar {
	padding: 0.5rem;
	border: var(--default-border);
	border-bottom-right-radius: 0;
	border-bottom-left-radius: 0;

	.add-row-button-container {
		width: 8rem;
	}
}

.table-editor-container {
	padding: 0.5rem;
	border: var(--default-border);
	border-top: none;
	border-top-right-radius: 0;
	border-top-left-radius: 0;

	table {
		border-collapse: collapse;
		width: 100%;
	}

	th {
		min-width: 3em;
		padding: 0.125em 0.25em;
		border: var(--default-border);
	}
}
</style>
