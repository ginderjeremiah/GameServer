<tr class:deleted={isDeleted}>
	<td class="edge-cell">
		<div
			class="status-edge"
			style:background={edgeColor}
			style:box-shadow={edgeColor === 'transparent' ? 'none' : `0 0 8px ${edgeColor}`}
		></div>
	</td>

	{#if !primaryKey}
		<td class="index-cell">{index}</td>
	{/if}

	{#each columns as { key, disabled } (key)}
		<TableCell
			bind:data={data[key]}
			disabled={disabled || isDeleted}
			selectOptions={selectOptions?.[key]?.(data)}
			noEditor={key === primaryKey}
			dirty={cellDirty(key)}
		/>
	{/each}

	<td class="actions-cell">
		<div class="actions">
			{#if isDeleted}
				<button type="button" class="row-action restore" onclick={onRestore}>Restore</button>
			{:else}
				{#if state === RowState.Modified}
					<button type="button" class="row-action reset" onclick={onReset}>Reset</button>
				{/if}
				<button type="button" class="row-action delete" onclick={onDelete}>Delete</button>
			{/if}
		</div>
	</td>
</tr>

<script lang="ts" generics="T extends object">
import type { ColumnData } from './types';
import { RowState, valuesEqual } from './types';
import TableCell from './TableCell.svelte';
import type { SelectOptions } from '$components';

interface Props<T extends object> {
	data: T;
	originalData: T;
	state: RowState;
	index: number;
	columns: ColumnData<T>[];
	primaryKey?: keyof T;
	selectOptions?: Partial<{ [key in keyof T]: (t: T) => SelectOptions }>;
	onDelete: () => void;
	onReset: () => void;
	onRestore: () => void;
}

let {
	data = $bindable(),
	originalData,
	state,
	index,
	columns,
	primaryKey,
	selectOptions,
	onDelete,
	onReset,
	onRestore
}: Props<T> = $props();

const isDeleted = $derived(state === RowState.Deleted || state === RowState.AddedDeleted);

const EDGE: Record<RowState, string> = {
	[RowState.Added]: '#a1c2f7',
	[RowState.Modified]: '#f0d28a',
	[RowState.Deleted]: '#e08a78',
	[RowState.AddedDeleted]: '#e08a78',
	[RowState.Unmodified]: 'transparent'
};
const edgeColor = $derived(EDGE[state]);

// A cell shows the dirty indicator only on an existing (non-added, non-deleted)
// row whose value differs from the saved original.
const cellDirty = (key: keyof T) => {
	if (state !== RowState.Modified && state !== RowState.Unmodified) return false;
	if (key === primaryKey) return false;
	return !valuesEqual(data[key], originalData[key]);
};
</script>

<style lang="scss">
tr {
	&.deleted {
		background: rgba(224, 138, 120, 0.05);

		.index-cell,
		:global(.cell) {
			opacity: 0.4;
		}
	}
}

.edge-cell {
	padding: 0;
	width: 28px;
	border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.status-edge {
	width: 3px;
	height: 30px;
	margin: 0 auto;
	transition:
		background 140ms ease,
		box-shadow 140ms ease;
}

.index-cell {
	padding: 6px 10px;
	border-bottom: 1px solid rgba(255, 255, 255, 0.05);
	font-family: var(--mono);
	font-size: 12px;
	color: rgba(240, 240, 240, 0.45);
	text-align: center;
}

.actions-cell {
	padding: 6px 16px 6px 10px;
	border-bottom: 1px solid rgba(255, 255, 255, 0.05);
	// Match the header so the column always fits Reset + Delete; a lone Delete
	// then sits centered in that reserved space.
	min-width: 150px;
}

.actions {
	display: flex;
	gap: 6px;
	justify-content: center;
}

.row-action {
	background: transparent;
	border: 1px solid rgba(255, 255, 255, 0.12);
	color: rgba(240, 240, 240, 0.55);
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 4px 10px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 130ms ease;

	&.delete:hover {
		border-color: #e08a78;
		color: #e08a78;
		background: rgba(224, 138, 120, 0.12);
	}

	&.restore:hover {
		border-color: var(--accent);
		color: var(--accent);
		background: rgba(161, 194, 247, 0.12);
	}

	&.reset:hover {
		border-color: var(--warning);
		color: var(--warning);
		background: rgba(240, 210, 138, 0.12);
	}
}
</style>
