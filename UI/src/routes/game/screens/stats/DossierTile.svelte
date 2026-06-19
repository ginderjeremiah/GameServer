<!-- One statistic in an entity's dossier: the entity's value for that stat, its
     rank among peers, a proportional bar, and how it compares to the leader.
     Clicking jumps back to that statistic's category. -->
<button type="button" class="tile" data-testid="dossier-tile-{info.stat.id}" onclick={() => onPickStat(info.stat.cat)}>
	<div class="tile-head">
		<span class="stat-name-cell">
			<span class="stat-name">{info.stat.name}</span>
			<CategoryTag cat={info.stat.cat} />
		</span>
		<span class="rank" class:top={isTop}>#{info.rank} / {info.of}</span>
	</div>
	<div class="value-row">
		<span class="value">{fmtValue(info.value, info.stat.unit)}</span>
		<CompHint stat={info.stat} />
	</div>
	<MiniBar frac={maxVal > 0 ? info.value / maxVal : 0} color={kindColor} height={5} />
	<span class="context">
		{#if isTop}
			Your highest for this stat
		{:else if top}
			vs. {top.entity.name} ({fmtValue(maxVal, info.stat.unit)})
		{/if}
	</span>
</button>

<script lang="ts">
import CategoryTag from './CategoryTag.svelte';
import CompHint from './CompHint.svelte';
import MiniBar from './MiniBar.svelte';
import type { EntityStatInfo, StatSummary, StatEntityKind } from './statistics-view.svelte';
import { fmtValue, statKindColor } from './statistics-display';

interface Props {
	info: EntityStatInfo;
	/** Memoised rows + bar max for this stat (computed on StatisticsData). */
	summary: StatSummary;
	kind: StatEntityKind;
	selId: number;
	onPickStat: (cat: EntityStatInfo['stat']['cat']) => void;
}

let { info, summary, kind, selId, onPickStat }: Props = $props();

const maxVal = $derived(summary.maxVal);
const top = $derived(summary.rows[0]);
const isTop = $derived(top?.entityId === selId);
const kindColor = $derived(statKindColor(kind));
</script>

<style lang="scss">
.tile {
	display: block;
	width: 100%;
	text-align: left;
	font-family: inherit;
	background: color-mix(in srgb, var(--white) 2.5%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 6px;
	padding: 13px 15px 14px;
	cursor: pointer;
	transition: all 110ms;

	&:hover {
		background: color-mix(in srgb, var(--white) 4.5%, transparent);
		border-color: var(--border-medium);
	}
}

.tile-head {
	display: flex;
	align-items: center;
	justify-content: space-between;
	gap: 8px;
	margin-bottom: 9px;
}

.stat-name-cell {
	display: flex;
	align-items: center;
	gap: 7px;
	min-width: 0;
}

.stat-name {
	font-size: 12.5px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.rank {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
	border: 1px solid var(--border-light);
	border-radius: 8px;
	padding: 1px 7px;
	white-space: nowrap;
	flex-shrink: 0;

	&.top {
		color: var(--accent);
		border-color: color-mix(in srgb, var(--accent) 45%, transparent);
	}
}

.value-row {
	display: flex;
	align-items: baseline;
	gap: 8px;
	margin-bottom: 9px;
}

.value {
	font-family: var(--sans);
	font-size: 22px;
	font-weight: 600;
	letter-spacing: -0.4px;
	color: var(--text-primary);
}

.context {
	display: block;
	margin-top: 6px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.4px;
	color: var(--text-muted);
}
</style>
