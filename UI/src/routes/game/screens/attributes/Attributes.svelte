<div class="attr-frame" data-testid="attributes-screen">
	<!-- header -->
	<div class="header">
		<div>
			<div class="eyebrow">Character</div>
			<h1 class="title">Attributes</h1>
		</div>
		<ModeToggle mode={view.mode} onPick={(m) => view.setMode(m)} />
	</div>

	<!-- budget -->
	<div class="budget-row">
		<BudgetMeter remaining={view.remaining} budget={view.budget} />
		<span class="hint">
			{view.mode === 'theory' ? 'Marginal yield shown per attribute' : 'Drag an axis or use − / + to spend'}
		</span>
	</div>

	<!-- main -->
	<div class="main" class:theory={view.mode === 'theory'}>
		{#if view.mode === 'theory'}
			<div class="alloc-table">
				<div class="table-head">
					<span></span>
					<span>Attribute</span>
					<span>Allocation</span>
					<span class="right">Total</span>
					<span></span>
				</div>
				<div class="table-body">
					{#each coreIndices as i (i)}
						<TheoryRow {i} {view} />
					{/each}
				</div>
			</div>
			<div class="theory-right">
				<div class="mini-radar">
					<AttributesRadar {view} size={244} />
				</div>
				<DerivedPanel {view} />
			</div>
		{:else}
			<div class="hero-radar">
				<AttributesRadar {view} size={430} />
			</div>
			<div class="steppers">
				{#each coreIndices as i (i)}
					<GuidedRow {i} {view} />
				{/each}
			</div>
		{/if}
	</div>

	<!-- commit -->
	<div class="footer">
		<CommitBar {view} />
	</div>

	<!-- Shared attribute tooltip for the steppers' icon/name, published via context so the rows
	     (guided or theory mode) don't have to thread hover handlers up to this owner. -->
	<AttributeTooltip bind:this={tooltip} attributeId={tip.attributeId} />
</div>

<script lang="ts">
import { onDestroy } from 'svelte';
import { type TooltipComponent } from '$stores';
import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';
import { createAttributeTooltip, setAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import ModeToggle from './ModeToggle.svelte';
import BudgetMeter from './BudgetMeter.svelte';
import AttributesRadar from './AttributesRadar.svelte';
import GuidedRow from './GuidedRow.svelte';
import TheoryRow from './TheoryRow.svelte';
import DerivedPanel from './DerivedPanel.svelte';
import CommitBar from './CommitBar.svelte';
import { AttributesView, CORE_ATTRIBUTES } from './attributes-view.svelte';

const view = new AttributesView();
const coreIndices = CORE_ATTRIBUTES.map((_, i) => i);

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);
setAttributeTooltip(tip.controller);

onDestroy(() => view.dispose());
</script>

<style lang="scss">
.attr-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	display: flex;
	align-items: flex-end;
	justify-content: space-between;
	gap: 16px;
	padding: 20px 28px 16px;
	flex-shrink: 0;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--accent) 70%, transparent);
	margin-bottom: 6px;
}

.title {
	margin: 0;
	font-size: 24px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.budget-row {
	display: flex;
	align-items: flex-end;
	justify-content: space-between;
	gap: 20px;
	padding: 0 28px 16px;
	flex-shrink: 0;
}

.hint {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	color: var(--text-muted);
}

.main {
	flex: 1;
	min-height: 0;
	padding: 0 28px;
	display: flex;
	gap: 28px;
	align-items: center;

	&.theory {
		gap: 18px;
		align-items: stretch;
	}
}

/* guided */
.hero-radar {
	flex-shrink: 0;
	display: flex;
	align-items: center;
	justify-content: center;
}

.steppers {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 8px;
}

/* theory */
.alloc-table {
	flex: 1.3 1 0;
	min-width: 0;
	align-self: stretch;
	display: flex;
	flex-direction: column;
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	background: color-mix(in srgb, var(--black) 25%, transparent);
	overflow: hidden;
}

.table-head {
	display: grid;
	grid-template-columns: 40px 1.7fr 1.3fr 0.8fr auto;
	gap: 14px;
	padding: 10px 16px;
	border-bottom: 1px solid var(--border-light);
	background: color-mix(in srgb, var(--white) 2%, transparent);

	span {
		font-family: var(--mono);
		font-size: 9px;
		letter-spacing: 1.3px;
		text-transform: uppercase;
		color: color-mix(in srgb, var(--text-primary) 42%, transparent);
	}

	.right {
		text-align: right;
	}
}

.table-body {
	flex: 1;
	display: flex;
	flex-direction: column;
}

.theory-right {
	flex: 1 1 0;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 14px;
}

.mini-radar {
	display: flex;
	justify-content: center;
	align-items: center;
	padding: 6px 0 2px;
	flex-shrink: 0;
}

.footer {
	padding: 16px 28px 20px;
	flex-shrink: 0;
}
</style>
