<tr>
	{#if !options?.primaryKey}
		<td>{index}</td>
	{/if}
	{#each columns as { key, disabled }}
		<TableCell
			bind:data={data[key]}
			{disabled}
			selectOptions={options.selectOptions?.[key]?.(data)}
			noEditor={key === options.primaryKey}
		/>
	{/each}
	<td>
		<div class="delete-button-container">
			<Button text="X" onClick={onDelete} textPadding="minimal" />
		</div>
	</td>
</tr>

<script lang="ts" generics="T extends {}">
import type { ColumnData, EditorOptions, RowData } from './';
import TableCell from './TableCell.svelte';
import { Button, RowState } from '$components';

interface Props extends RowData<T> {
	options: EditorOptions<T>;
	columns: ColumnData<T>[];
}

let {
	data = $bindable(),
	state = $bindable(),
	index,
	originalData,
	options,
	columns
}: Props = $props();

//const reactiveData = $derived(data);

const onDelete = () => {
	if (state === RowState.Added) {
		state = RowState.AddedDeleted;
	} else {
		state = RowState.Deleted;
	}
};

$effect(() => {
	if (state !== RowState.Deleted && state !== RowState.Added && state !== RowState.AddedDeleted) {
		let isModified = false;
		for (const { key } of columns) {
			isModified ||= data[key] !== originalData[key];
		}

		state = isModified ? RowState.Modified : RowState.Unmodified;
	}
});
</script>

<style lang="scss">
.delete-button-container {
	width: 1.5rem;
	margin: 0 auto;
}

td {
	text-align: center;
	padding: 0.125em 0.25em;
	border: var(--default-border);
}
</style>
