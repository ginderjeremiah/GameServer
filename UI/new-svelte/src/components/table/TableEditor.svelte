<div class="workspace">
	<div class="workspace-header">
		<div>
			<div class="eyebrow">Admin · Tools</div>
			{#if title}
				<h1 class="workspace-title">{title}</h1>
			{/if}
		</div>
		<button type="button" class="adm-btn ghost" onclick={addNewRow}>
			<svg
				width="12"
				height="12"
				viewBox="0 0 12 12"
				fill="none"
				stroke="currentColor"
				stroke-width="1.6"
				stroke-linecap="round"
				aria-hidden="true"
			>
				<path d="M6 2v8M2 6h8" />
			</svg>
			Add Row
		</button>
	</div>

	{#if gate}
		<div class="workspace-gate">{@render gate()}</div>
	{/if}

	<div class="table-scroll">
		<table>
			<thead>
				<tr>
					<th class="edge-th"></th>
					{#if !primaryKey}
						<th>Index</th>
					{/if}
					{#each columns as column (column.key)}
						<th>{column.name}</th>
					{/each}
					<th class="actions-th">Actions</th>
				</tr>
			</thead>
			<tbody>
				{#each visibleRows as row (row.index)}
					<TableRow
						bind:data={row.data}
						originalData={row.originalData}
						state={rowState(row)}
						index={row.index}
						{columns}
						{primaryKey}
						{selectOptions}
						onDelete={() => removeRow(row)}
						onReset={() => resetRow(row)}
						onRestore={() => (row.deleted = false)}
					/>
				{/each}
			</tbody>
		</table>
	</div>

	<div class="save-bar">
		<div class="save-summary">
			{#if savedFlash}
				<span class="saved-flash">
					<svg
						width="13"
						height="13"
						viewBox="0 0 14 14"
						fill="none"
						stroke="currentColor"
						stroke-width="1.6"
						aria-hidden="true"
					>
						<path d="M3 7.3L6 10.2L11 4" stroke-linecap="round" stroke-linejoin="round" />
					</svg>
					Changes saved
				</span>
			{:else if pendingCount === 0}
				<span class="no-changes">No unsaved changes</span>
			{:else}
				<span class="pending-count">{pendingCount} unsaved {pendingCount === 1 ? 'change' : 'changes'}</span>
				<span class="change-pips">
					{#if counts.added}<span class="pip added"><span class="dot"></span>{counts.added} added</span>{/if}
					{#if counts.modified}<span class="pip modified"><span class="dot"></span>{counts.modified} edited</span>{/if}
					{#if counts.deleted}<span class="pip deleted"><span class="dot"></span>{counts.deleted} removed</span>{/if}
				</span>
			{/if}
		</div>
		<div class="save-actions">
			<button type="button" class="adm-btn ghost" disabled={pendingCount === 0} onclick={discard}>Discard</button>
			<button type="button" class="adm-btn primary" disabled={pendingCount === 0 || saving} onclick={save}
				>Save Changes</button
			>
		</div>
	</div>
</div>

<script lang="ts" generics="T extends object">
import { keys, normalizeText } from '$lib/common';
import { type EditorOptions, RowState, valuesEqual } from './types';
import TableRow from './TableRow.svelte';
import { EChangeType, type IChange } from '$lib/api';

export const getChanges = getRowChanges;
export const getRowData = getModifiedRowData;

interface Row<T extends object> {
	index: number;
	data: T;
	originalData: T;
	added: boolean;
	deleted: boolean;
}

const {
	data,
	primaryKey,
	hiddenColumns,
	disabledColumns,
	selectOptions,
	sampleItem,
	title,
	onSave,
	gate
}: EditorOptions<T> = $props();

let rows = $state<Row<T>[]>([]);
let savedFlash = $state(false);
let saving = $state(false);
let newItemIndex = -1;

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

const editableColumns = $derived(columns.filter((c) => c.key !== primaryKey));

// Row state is DERIVED from comparing each cell to its saved original, so
// undo-after-edit returns to the correct modified/clean state and reverting
// every cell returns the row to clean.
const isDirty = (row: Row<T>) => editableColumns.some((c) => !valuesEqual(row.data[c.key], row.originalData[c.key]));

const rowState = (row: Row<T>): RowState =>
	row.deleted
		? row.added
			? RowState.AddedDeleted
			: RowState.Deleted
		: row.added
			? RowState.Added
			: isDirty(row)
				? RowState.Modified
				: RowState.Unmodified;

// Newly-added rows that get deleted simply disappear; existing deleted rows
// stay visible (dimmed) so they can be restored.
const visibleRows = $derived(rows.filter((r) => !(r.added && r.deleted)));

const counts = $derived.by(() => {
	let added = 0;
	let modified = 0;
	let deleted = 0;
	for (const row of rows) {
		switch (rowState(row)) {
			case RowState.Added:
				added++;
				break;
			case RowState.Modified:
				modified++;
				break;
			case RowState.Deleted:
				deleted++;
				break;
		}
	}
	return { added, modified, deleted };
});

const pendingCount = $derived(counts.added + counts.modified + counts.deleted);

const getKeyName = (key: string | number | symbol) => {
	let keyName = key.toString();
	if (keyName.endsWith('Id') && keyName !== primaryKey) {
		keyName = keyName.slice(0, -2);
	}
	return normalizeText(keyName);
};

const buildRows = () => {
	const validData = data?.length && !data[0] ? data.slice(1) : data;
	rows =
		validData?.map((d, i) => ({
			index: i,
			data: { ...d },
			originalData: { ...d },
			added: false,
			deleted: false
		})) ?? [];
	newItemIndex = -1;
};

const addNewRow = () => {
	const base = (sampleItem ?? rows[0]?.originalData ?? {}) as T;
	const newData = { ...base } as T;
	if (primaryKey) {
		newData[primaryKey] = newItemIndex as T[keyof T];
	}
	rows.push({
		index: newItemIndex,
		data: newData,
		originalData: { ...base },
		added: true,
		deleted: false
	});
	newItemIndex--;
};

const removeRow = (row: Row<T>) => {
	if (row.added) {
		const i = rows.indexOf(row);
		if (i >= 0) rows.splice(i, 1);
	} else {
		row.deleted = true;
	}
};

const resetRow = (row: Row<T>) => {
	for (const c of editableColumns) {
		row.data[c.key] = row.originalData[c.key];
	}
};

const save = async () => {
	saving = true;
	try {
		await onSave?.();
		savedFlash = true;
		setTimeout(() => (savedFlash = false), 1700);
	} finally {
		saving = false;
	}
};

const discard = () => buildRows();

function getRowChanges() {
	const changes: IChange<T>[] = [];
	for (const row of rows) {
		switch (rowState(row)) {
			case RowState.Added:
				changes.push({ changeType: EChangeType.Add, item: $state.snapshot(row.data) as T });
				break;
			case RowState.Deleted:
				changes.push({ changeType: EChangeType.Delete, item: $state.snapshot(row.data) as T });
				break;
			case RowState.Modified:
				changes.push({ changeType: EChangeType.Edit, item: $state.snapshot(row.data) as T });
				break;
		}
	}
	return changes;
}

function getModifiedRowData() {
	return rows.filter((r) => !r.deleted).map((r) => $state.snapshot(r.data) as T);
}

// Rebuild whenever the source data changes (initial load, gate change, or a
// refetch after saving), resetting all row/cell state to clean.
$effect(() => {
	buildRows();
});
</script>

<style lang="scss">
.workspace {
	display: flex;
	flex-direction: column;
	min-height: 0;
	flex: 1;
}

.workspace-header {
	display: flex;
	align-items: flex-end;
	justify-content: space-between;
	gap: 16px;
	margin-bottom: 18px;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--accent) 70%, transparent);
	margin-bottom: 6px;
}

.workspace-title {
	margin: 0;
	padding: 0;
	font-size: 26px;
	font-weight: 500;
	color: var(--text-primary);
	letter-spacing: -0.2px;
}

.workspace-gate {
	margin-bottom: 18px;
}

.table-scroll {
	flex: 1;
	min-height: 0;
	overflow: auto;
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 1.5%, transparent);
	width: fit-content;
	margin: auto;

	table {
		border-collapse: collapse;
		// Size to content instead of stretching to the container, so a table
		// with few/narrow columns doesn't span the whole page.
		width: auto;
		min-width: max-content;
	}

	th {
		font-family: var(--mono);
		font-size: 9.5px;
		font-weight: 400;
		letter-spacing: 1.4px;
		text-transform: uppercase;
		color: color-mix(in srgb, var(--text-primary) 50%, transparent);
		text-align: center;
		padding: 11px 10px;
		border-bottom: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
		background: color-mix(in srgb, var(--surface) 92%, transparent);
		position: sticky;
		top: 0;
		z-index: 1;
		white-space: nowrap;
	}

	.edge-th {
		width: 28px;
		padding: 0;
	}

	.actions-th {
		// Reserve enough room for the Reset + Delete pair so the column width
		// stays stable whether or not the Reset button is showing.
		min-width: 150px;
		padding-right: 16px;
	}
}

.save-bar {
	display: flex;
	align-items: center;
	justify-content: space-between;
	margin-top: 16px;
	padding-top: 16px;
	border-top: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
}

.save-summary {
	display: flex;
	align-items: center;
	gap: 16px;
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.4px;
	color: var(--text-tertiary);
}

.saved-flash {
	color: var(--success);
	display: inline-flex;
	align-items: center;
	gap: 7px;
}

.pending-count {
	color: var(--text-secondary);
}

.change-pips {
	display: inline-flex;
	gap: 12px;
}

.pip {
	display: inline-flex;
	align-items: center;
	gap: 6px;

	.dot {
		width: 6px;
		height: 6px;
		border-radius: 50%;
	}

	&.added {
		color: var(--change-added);
		.dot {
			background: var(--change-added);
			box-shadow: 0 0 6px var(--change-added);
		}
	}

	&.modified {
		color: var(--change-modified);
		.dot {
			background: var(--change-modified);
			box-shadow: 0 0 6px var(--change-modified);
		}
	}

	&.deleted {
		color: var(--change-removed);
		.dot {
			background: var(--change-removed);
			box-shadow: 0 0 6px var(--change-removed);
		}
	}
}

.save-actions {
	display: flex;
	gap: 10px;
}

.adm-btn {
	display: inline-flex;
	align-items: center;
	gap: 7px;
	background: transparent;
	border: 1px solid var(--border-light);
	color: color-mix(in srgb, var(--text-primary) 85%, transparent);
	font-family: var(--mono);
	font-size: 11.5px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 8px 16px;
	border-radius: 3px;
	cursor: pointer;
	transition: all 140ms ease;
	white-space: nowrap;

	&.ghost:hover:not(:disabled) {
		border-color: color-mix(in srgb, var(--white) 32%, transparent);
		box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 45%, transparent);
	}

	&.primary {
		background: color-mix(in srgb, var(--accent) 12%, transparent);
		border-color: var(--accent);
		color: var(--accent-light);

		&:hover:not(:disabled) {
			box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 50%, transparent);
		}
	}

	&:disabled {
		color: color-mix(in srgb, var(--text-primary) 30%, transparent);
		border-color: var(--border-subtle);
		cursor: not-allowed;
	}
}
</style>
