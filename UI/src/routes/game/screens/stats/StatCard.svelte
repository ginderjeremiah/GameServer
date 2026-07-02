<!-- A statistic preview card: its name, category + kind tags, the grand-total
     headline, and a compact top-entities breakdown (or an "overall only" note
     for total-only statistics). -->
<div class="card" data-testid="stat-card-{stat.id}">
	<div class="card-head">
		<div class="heading">
			<div class="title-row">
				<span class="stat-name">{stat.name}</span>
				<CategoryTag cat={stat.cat} />
			</div>
			<KindBadge kind={stat.kind} />
		</div>
		<div class="headline">
			<div class="value">{fmtValue(headline, stat.unit)}</div>
			<CompHint {stat} />
		</div>
	</div>

	<div class="breakdown">
		{#if stat.kind === 'none'}
			<span class="overall-note">Tracked overall — no per-entity breakdown.</span>
		{:else}
			{#each top as row (row.entityId)}
				<StatCardRow {row} {stat} kind={asKind} {maxVal} onPick={handlePick} />
			{/each}
			{#if more > 0}
				<span class="more">+{more} more {statKindPlural(asKind).toLowerCase()}</span>
			{/if}
		{/if}
	</div>
</div>

<script lang="ts">
import CategoryTag from './CategoryTag.svelte';
import KindBadge from './KindBadge.svelte';
import CompHint from './CompHint.svelte';
import StatCardRow from './StatCardRow.svelte';
import type { StatSummary, StatType, StatEntityKind, StatBreakdownKind } from './statistics-view.svelte';
import { fmtValue, statKindPlural } from './statistics-display';

interface Props {
	/** Memoised rows + bar max + headline for this stat (computed on StatisticsData). */
	summary: StatSummary;
	stat: StatType;
	onPickEntity: (kind: StatEntityKind, id: number) => void;
}

let { summary, stat, onPickEntity }: Props = $props();

const headline = $derived(summary.headline);
const maxVal = $derived(summary.maxVal);
const top = $derived(summary.rows.slice(0, 4));
const more = $derived(summary.rows.length - top.length);
// Safe to read only when kind !== 'none' (guarded in the template).
const asKind = $derived(stat.kind as StatBreakdownKind);

// The damage-type breakdown has no dossier to navigate to, so its rows don't call back.
const handlePick = (id: number): void => {
	if (asKind !== 'damageType') {
		onPickEntity(asKind, id);
	}
};
</script>

<style lang="scss">
.card {
	background: color-mix(in srgb, var(--white) 2.5%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 6px;
	padding: 15px 17px 16px;
	display: flex;
	flex-direction: column;
}

.card-head {
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	gap: 12px;
	margin-bottom: 13px;
}

.title-row {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}

.stat-name {
	font-size: 15px;
	font-weight: 500;
	color: var(--text-primary);
}

.headline {
	text-align: right;
	white-space: nowrap;
}

.value {
	font-family: var(--sans);
	font-size: 24px;
	font-weight: 600;
	letter-spacing: -0.6px;
	color: var(--text-primary);
}

.breakdown {
	border-top: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
	padding-top: 9px;
}

.more {
	display: block;
	margin-top: 6px;
	padding-left: 19px;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-muted);
}

.overall-note {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.3px;
	color: var(--text-muted);
}
</style>
