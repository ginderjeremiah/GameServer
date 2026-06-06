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
	[RowState.Added]: 'var(--accent)',
	[RowState.Modified]: 'var(--warning)',
	[RowState.Deleted]: 'var(--enemy-accent)',
	[RowState.AddedDeleted]: 'var(--enemy-accent)',
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
		background: color-mix(in srgb, var(--enemy-accent) 5%, transparent);

		.index-cell,
		:global(.cell) {
			opacity: 0.4;
		}
	}
}

.edge-cell {
	padding: 0;
	width: 28px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
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
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
	font-family: var(--mono);
	font-size: 12px;
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
	text-align: center;
}

.actions-cell {
	padding: 6px 16px 6px 10px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
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
	border: 1px solid color-mix(in srgb, var(--white) 12%, transparent);
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 4px 10px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 130ms ease;

	&.delete:hover {
		border-color: var(--enemy-accent);
		color: var(--enemy-accent);
		background: color-mix(in srgb, var(--enemy-accent) 12%, transparent);
	}

	&.restore:hover {
		border-color: var(--accent);
		color: var(--accent);
		background: color-mix(in srgb, var(--accent) 12%, transparent);
	}

	&.reset:hover {
		border-color: var(--warning);
		color: var(--warning);
		background: color-mix(in srgb, var(--warning) 12%, transparent);
	}
}
</style>
