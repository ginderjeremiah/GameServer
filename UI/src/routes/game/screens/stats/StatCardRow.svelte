<!-- One per-item row inside a stat card: name, a proportional mini bar, and the value.
     Enemy/zone/skill rows are buttons that pivot into that entity's Codex dossier; the
     damage-type breakdown has no dossier to pivot into (#1473), so those rows render as a
     flat, non-interactive list instead. -->
{#if kind === 'damageType'}
	<div class="row static" data-testid="stat-row-{stat.id}-{row.entityId}">
		<span class="name-cell">
			<DamageTypeIcon dmgKey={row.entityId} size={12} />
			<span class="name">{row.entity.name}</span>
		</span>
		<MiniBar frac={maxVal > 0 ? row.value / maxVal : 0} color={kindColor} height={5} />
		<span class="value">{fmtValue(row.value, stat.unit)}</span>
	</div>
{:else}
	<button
		type="button"
		class="row"
		data-testid="stat-row-{stat.id}-{row.entityId}"
		onclick={() => onPick(row.entityId)}
	>
		<span class="name-cell">
			<StatGlyph {kind} size={12} />
			<span class="name">{row.entity.name}</span>
			<svg class="chev" width="10" height="10" viewBox="0 0 12 12" fill="none" stroke={kindColor} stroke-width="1.5">
				<path d="M4 2.5L8 6l-4 3.5" stroke-linecap="round" stroke-linejoin="round" />
			</svg>
		</span>
		<MiniBar frac={maxVal > 0 ? row.value / maxVal : 0} color={kindColor} height={5} />
		<span class="value">{fmtValue(row.value, stat.unit)}</span>
	</button>
{/if}

<script lang="ts">
import type { EDamageTypeKey } from '$lib/api';
import { damageTypeKeyColor } from '$lib/common';
import DamageTypeIcon from '$components/DamageTypeIcon.svelte';
import StatGlyph from './StatGlyph.svelte';
import MiniBar from './MiniBar.svelte';
import type { StatRow, StatType, StatBreakdownKind } from './statistics-view.svelte';
import { fmtValue, statKindColor } from './statistics-display';

interface Props {
	row: StatRow;
	stat: StatType;
	kind: StatBreakdownKind;
	maxVal: number;
	/** Ignored for the non-interactive damage-type rows. */
	onPick: (entityId: number) => void;
}

let { row, stat, kind, maxVal, onPick }: Props = $props();

const kindColor = $derived(
	kind === 'damageType' ? damageTypeKeyColor(row.entityId as EDamageTypeKey) : statKindColor(kind)
);
</script>

<style lang="scss">
.row {
	width: 100%;
	display: grid;
	grid-template-columns: 1fr 96px 70px;
	gap: 10px;
	align-items: center;
	padding: 4px 6px;
	margin: 0 -6px;
	border: none;
	border-radius: 4px;
	background: transparent;
	cursor: pointer;
	font-family: inherit;
	text-align: left;

	&.static {
		cursor: default;
	}

	&:not(.static):hover {
		background: color-mix(in srgb, var(--white) 4.5%, transparent);

		.name {
			color: var(--text-primary);
		}

		.chev {
			opacity: 1;
		}
	}
}

.name-cell {
	display: flex;
	align-items: center;
	gap: 7px;
	min-width: 0;
}

.name {
	font-size: 12px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.chev {
	flex-shrink: 0;
	opacity: 0;
	transition: opacity 110ms;
}

.value {
	font-family: var(--mono);
	font-size: 11.5px;
	text-align: right;
	color: var(--text-secondary);
}
</style>
