<!-- Attribute Breakdown screen — a read-only inspector that traces every
     implemented attribute back to its sources (base / stat points / gear / mods
     / derived). Left rail picks an attribute; the right panel decomposes it. -->
<div class="breakdown-frame" data-testid="attribute-breakdown-screen">
	<div class="header">
		<div class="eyebrow">Character · Attribute Breakdown</div>
		<div class="title-line">
			<h1 class="title">Attributes</h1>
			<span class="sub">{playerManager.name} · Level {playerManager.level} — trace where each value comes from</span>
		</div>
	</div>

	<div class="body">
		<AttributeList {view} />
		<BreakdownDetail {view} />
	</div>

	<!-- Shared attribute tooltip for the rail rows (and any future attribute surface on this screen),
	     published via context so the rows don't have to thread hover handlers up to this owner. -->
	<AttributeTooltip bind:this={tooltip} attributeId={tip.attributeId} effect={tip.effect} />
</div>

<script lang="ts">
import { playerManager } from '$lib/engine';
import { type TooltipComponent } from '$stores';
import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';
import { createAttributeTooltip, setAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import AttributeList from './AttributeList.svelte';
import BreakdownDetail from './BreakdownDetail.svelte';
import { AttributeBreakdownView } from './attribute-breakdown-view.svelte';

const view = new AttributeBreakdownView();

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);
setAttributeTooltip(tip.controller);
</script>

<style lang="scss">
.breakdown-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	padding: 20px 28px 16px;
	flex-shrink: 0;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--eyebrow);
	margin-bottom: 6px;
}

.title-line {
	display: flex;
	align-items: baseline;
	gap: 12px;
	flex-wrap: wrap;
}

.title {
	margin: 0;
	font-size: 23px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.sub {
	font-size: 12.5px;
	color: var(--text-tertiary);
}

.body {
	flex: 1;
	min-height: 0;
	display: grid;
	grid-template-columns: 264px 1fr;
	gap: 22px;
	padding: 16px 28px 28px;
}
</style>
