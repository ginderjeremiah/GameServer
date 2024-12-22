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
		<div class="center-content">
			<div class="delete-button-container">
				<Button text="Delete" onClick={onDelete} textPadding="minimal" />
			</div>
		</div>
	</td>
</tr>

<script lang="ts" generics="T extends {}">
import type { ColumnData, EditorOptions, RowData } from './';
import TableCell from './TableCell.svelte';
import { Button, RowState } from '$components';
import { untrack } from 'svelte';

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

const onDelete = () => {
	if (state === RowState.Added) {
		state = RowState.AddedDeleted;
	} else {
		state = RowState.Deleted;
	}
};

$effect(() => {
	if (untrack(() => state === RowState.Modified || state === RowState.Unmodified)) {
		let isModified = columns.some(({ key }) => data[key] !== originalData[key]);
		state = isModified ? RowState.Modified : RowState.Unmodified;
	}
});
</script>

<style lang="scss">
.delete-button-container {
	width: fit-content;
}

td {
	text-align: center;
	padding: 0.125em 0.25em;
	border: var(--default-border);
}
</style>
