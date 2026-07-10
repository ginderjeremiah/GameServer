{#if col.type === 'select'}
	<td style:min-width="{col.min ?? 160}px">
		<div class="fld">
			<select class="sel" class:dirty value={row[col.key] as number} onchange={(e) => onChange(+e.currentTarget.value)}>
				{#each col.options?.(row[col.key] as number, row) ?? [] as option (option.value)}
					<option value={option.value} disabled={taken.has(option.value) && option.value !== row[col.key]}>
						{option.text}
					</option>
				{/each}
			</select>
			<SelectCaret />
			{#if dirty}<DirtyDot />{/if}
		</div>
	</td>
{:else if col.type === 'attribute'}
	<td style:min-width="{col.min ?? 160}px">
		<AttributePicker
			value={row[col.key] as number}
			options={col.options?.(row[col.key] as number, row) ?? []}
			onChange={(v) => onChange(v)}
			ariaLabel={col.label}
			disabledValues={col.unique ? taken : undefined}
			{dirty}
		/>
	</td>
{:else if col.type === 'number'}
	<td class:r={col.align === 'r'} style:width="{col.width ?? 110}px">
		<div class="fld">
			<NumInput
				class="inp num{dirty ? ' dirty' : ''}"
				value={(row[col.key] as number) ?? 0}
				allowNegative={col.allowNegative}
				onChange={(n) => onChange(n)}
			/>
			{#if dirty}<DirtyDot />{/if}
		</div>
	</td>
{:else if col.type === 'text'}
	<td style:min-width="{col.min ?? 200}px">
		<div class="fld">
			<input
				class="inp"
				class:dirty
				aria-label={col.label}
				placeholder={col.placeholder}
				value={(row[col.key] as string) ?? ''}
				oninput={(e) => onChange(e.currentTarget.value)}
			/>
			{#if dirty}<DirtyDot />{/if}
		</div>
	</td>
{:else}
	<td style:width="{col.width ?? 150}px">
		<div class="share">
			<div class="share-bar"><Bar presentational value={pct} /></div>
			<span class="share-pct">{pct}%</span>
		</div>
	</td>
{/if}

<script lang="ts">
import type { ColumnConfig } from '../entities/types';
import Bar from '$components/Bar.svelte';
import NumInput from './NumInput.svelte';
import SelectCaret from './SelectCaret.svelte';
import DirtyDot from './DirtyDot.svelte';
import AttributePicker from './AttributePicker.svelte';

interface Props {
	col: ColumnConfig;
	row: Record<string, number | string>;
	idx: number;
	rows: Record<string, number | string>[];
	record: unknown;
	dirty: boolean;
	onChange: (value: number | string) => void;
}

const { col, row, idx, rows, record, dirty, onChange }: Props = $props();

// For unique select/attribute columns, options already chosen in sibling rows are disabled.
// These columns' values are always numeric ids, unlike a free-form `text` column's.
const taken = $derived(
	col.unique ? new Set(rows.filter((_, ri) => ri !== idx).map((r) => r[col.key] as number)) : new Set<number>()
);

// Share columns: denominator is the sibling-row sum unless the column overrides it
// (an enemy's spawn share competes against all enemies in the zone).
const pct = $derived.by(() => {
	if (col.type !== 'share') {
		return 0;
	}
	const weightKey = col.weightKey ?? 'weight';
	const total = col.shareTotal
		? col.shareTotal(row, rows, record)
		: rows.reduce((sum, r) => sum + (Number(r[weightKey]) || 0), 0) || 1;
	return total > 0 ? Math.round(((Number(row[weightKey]) || 0) / total) * 100) : 0;
});
</script>

<style lang="scss">
.share {
	display: flex;
	align-items: center;
	gap: 9px;

	// Sizing wrapper for the presentational share fill; the Bar primitive supplies the visuals.
	.share-bar {
		flex: 1;
		--bar-height: 4px;
		--bar-radius: 2px;
		--bar-track-bg: color-mix(in srgb, var(--accent) 20%, transparent);
		--bar-fill: var(--accent);
		--bar-fill-shadow: 0 0 6px color-mix(in srgb, var(--accent) 60%, transparent);
		--bar-transition: none;
	}
}
</style>
