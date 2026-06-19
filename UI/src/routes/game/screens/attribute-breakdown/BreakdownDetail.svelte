<!-- The right-hand detail: the selected attribute blown up — its big total, a
     full-width stacked bar, a source legend, and the by-source + apply-order
     decompositions. -->
<div class="detail" data-testid="breakdown-detail">
	<div class="head">
		<div class="heading">
			<div class="title-row">
				<AttributeIcon id={view.selected} size={26} />
				<h2 class="name">{attributeName(view.selected, staticData.attributes)}</h2>
				<KindTag type={view.selectedMeta.type} />
			</div>
			{#if description}
				<p class="desc">{description}</p>
			{/if}
		</div>
		<div class="value">{fmtNum(computed.total, view.selectedMeta.dec, view.selectedMeta.pct)}</div>
	</div>

	<div class="bar"><StackBar {computed} grouped={view.selectedGrouped} height={18} /></div>
	<div class="legend"><SourceLegend sources={groups.map((g) => g.source)} /></div>

	{#if computed.lines.length === 0}
		<p class="empty">
			No contributors. This attribute is not currently produced by any base value, allocation, equipment, or derivation.
		</p>
	{:else}
		<div class="columns">
			<BySourceBreakdown {groups} pct={view.selectedMeta.pct} />
			<ApplyOrderTrace {computed} dec={view.selectedMeta.dec} pct={view.selectedMeta.pct} />
		</div>
	{/if}
</div>

<script lang="ts">
import StackBar from './StackBar.svelte';
import SourceLegend from './SourceLegend.svelte';
import KindTag from './KindTag.svelte';
import BySourceBreakdown from './BySourceBreakdown.svelte';
import ApplyOrderTrace from './ApplyOrderTrace.svelte';
import { attributeName } from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import { attributeDescription, fmtNum, type AttributeBreakdownView } from './attribute-breakdown-view.svelte';

interface Props {
	view: AttributeBreakdownView;
}

let { view }: Props = $props();

const computed = $derived(view.selectedComputed);
const groups = $derived(view.selectedGrouped.groups);
const description = $derived(attributeDescription(view.selected));
</script>

<style lang="scss">
.detail {
	overflow: auto;
	padding-right: 6px;
}

.head {
	display: flex;
	align-items: flex-start;
	justify-content: space-between;
	gap: 16px;
	margin-bottom: 6px;
}

.title-row {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-bottom: 6px;
}

.name {
	margin: 0;
	font-size: 24px;
	font-weight: 500;
	letter-spacing: -0.4px;
}

.desc {
	margin: 0;
	font-size: 12.5px;
	line-height: 1.5;
	color: var(--text-tertiary);
	max-width: 460px;
}

.value {
	font-family: var(--sans);
	font-size: 40px;
	font-weight: 600;
	letter-spacing: -1.2px;
	color: var(--text-primary);
	text-shadow: 0 0 22px color-mix(in srgb, var(--accent) 20%, transparent);
	white-space: nowrap;
}

.bar {
	margin: 14px 0 12px;
}

.legend {
	margin-bottom: 20px;
}

.empty {
	margin: 0;
	font-size: 11.5px;
	line-height: 1.5;
	color: var(--text-muted);
	max-width: 460px;
}

.columns {
	display: grid;
	grid-template-columns: 1fr 1fr;
	gap: 26px;
}

@media (max-width: 860px) {
	.columns {
		grid-template-columns: 1fr;
		gap: 18px;
	}
}
</style>
