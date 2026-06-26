{#if col.type === 'select'}
	<td style:min-width="{col.min ?? 160}px">
		<div class="fld">
			<select class="sel" class:dirty value={row[col.key]} onchange={(e) => onChange(+e.currentTarget.value)}>
				{#each col.options?.(row[col.key]) ?? [] as option (option.value)}
					<option value={option.value} disabled={taken.has(option.value) && option.value !== row[col.key]}>
						{option.text}
					</option>
				{/each}
			</select>
			<SelectCaret />
			{#if dirty}<DirtyDot />{/if}
		</div>
	</td>
{:else if col.type === 'number'}
	<td class:r={col.align === 'r'} style:width="{col.width ?? 110}px">
		<div class="fld">
			<NumInput
				class="inp num{dirty ? ' dirty' : ''}"
				value={row[col.key] ?? 0}
				allowNegative={col.allowNegative}
				onChange={(n) => onChange(n)}
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

interface Props {
	col: ColumnConfig;
	row: Record<string, number>;
	idx: number;
	rows: Record<string, number>[];
	record: unknown;
	dirty: boolean;
	onChange: (value: number) => void;
}

const { col, row, idx, rows, record, dirty, onChange }: Props = $props();

// For unique select columns, options already chosen in sibling rows are disabled.
const taken = $derived(
	col.unique ? new Set(rows.filter((_, ri) => ri !== idx).map((r) => r[col.key])) : new Set<number>()
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
		: rows.reduce((sum, r) => sum + (r[weightKey] || 0), 0) || 1;
	return total > 0 ? Math.round(((row[weightKey] || 0) / total) * 100) : 0;
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
