<!-- One per-entity row inside a stat card: entity name, a proportional mini bar,
     and the value. A button — clicking pivots to that entity's dossier. -->
<button type="button" class="row" data-testid="stat-row-{stat.id}-{row.entityId}" onclick={() => onPick(row.entityId)}>
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

<script lang="ts">
import StatGlyph from './StatGlyph.svelte';
import MiniBar from './MiniBar.svelte';
import type { StatRow, StatType, StatEntityKind } from './statistics-view.svelte';
import { fmtValue, statKindColor } from './statistics-display';

interface Props {
	row: StatRow;
	stat: StatType;
	kind: StatEntityKind;
	maxVal: number;
	onPick: (entityId: number) => void;
}

let { row, stat, kind, maxVal, onPick }: Props = $props();

const kindColor = $derived(statKindColor(kind));
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

	&:hover {
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
