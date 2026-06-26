<div class="row gap14">
	<ProgNumber
		label="Max level"
		value={tier.maxLevel}
		warn={tier.maxLevel < 1}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.maxLevel = Math.round(v)))}
	/>
	<ProgNumber
		label="Base XP"
		value={tier.baseXp}
		warn={!(tier.baseXp > 0)}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.baseXp = v))}
	/>
	<ProgNumber
		label="XP growth ×"
		value={tier.xpGrowth}
		warn={!(tier.xpGrowth > 0)}
		onChange={(v) => store.patchProf(tier.id, (d) => (d.xpGrowth = v))}
	/>
</div>

<div class="curve-label">Derived per-level cost <span class="muted">— baseXp × growth^(n−1), not stored</span></div>
<div class="chart">
	<div class="bars">
		{#each bars as bar (bar.level)}
			<div
				class="bar"
				class:last={bar.level === bars.length}
				style:height={`${bar.pct}%`}
				title={`lv ${bar.level}: ${bar.cost}`}
			></div>
		{/each}
	</div>
	<div class="bar-labels">
		{#each bars as bar (bar.level)}
			<div class="bl">{bar.label}</div>
		{/each}
	</div>
</div>

<script lang="ts">
import type { ProgressionStore } from './progression-store.svelte';
import { xpCostCurve } from './progression-helpers';
import type { WorkbenchProficiency } from './types';
import ProgNumber from './ProgNumber.svelte';

interface Props {
	store: ProgressionStore;
	tier: WorkbenchProficiency;
}

const { store, tier }: Props = $props();

const bars = $derived.by(() => {
	const max = Math.max(1, Math.min(40, Math.floor(tier.maxLevel) || 1));
	const costs = xpCostCurve(tier.baseXp, tier.xpGrowth, max);
	const peak = Math.max(...costs, 1);
	return costs.map((cost, i) => ({
		level: i + 1,
		cost,
		label: cost >= 1000 ? `${(cost / 1000).toFixed(1)}k` : `${cost}`,
		pct: Math.max(8, Math.round((cost / peak) * 100))
	}));
});
</script>

<style lang="scss">
.row {
	display: flex;
	margin-bottom: 20px;
}
.gap14 {
	gap: 14px;
}
.row :global(.numfld) {
	flex: 1;
}
.curve-label {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	margin-bottom: 12px;

	.muted {
		text-transform: none;
		letter-spacing: 0;
	}
}
.chart {
	background: var(--panel);
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	padding: 20px;
}
.bars {
	display: flex;
	align-items: flex-end;
	gap: 8px;
	height: 150px;
	border-bottom: 1px solid var(--border-light);
	padding: 0 2px;
}
.bar {
	flex: 1;
	border-radius: 2px 2px 0 0;
	background: color-mix(in srgb, var(--white) 22%, transparent);

	&.last {
		background: var(--accent);
	}
}
.bar-labels {
	display: flex;
	gap: 8px;
	padding: 8px 2px 0;
}
.bl {
	flex: 1;
	text-align: center;
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-tertiary);
}
</style>
