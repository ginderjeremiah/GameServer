<div class="trow">
	<span
		class="attr-hit"
		role="img"
		aria-label={name}
		onmouseenter={showTip}
		onmousemove={moveTip}
		onmouseleave={hideTip}
	>
		<AttributeIcon {id} size={40} />
	</span>
	<div class="attr">
		<!-- svelte-ignore a11y_no_static_element_interactions -->
		<div class="head" onmouseenter={showTip} onmousemove={moveTip} onmouseleave={hideTip}>
			<span class="code" style="color: {color}">{code}</span>
			<span class="name">{name}</span>
		</div>
		<div class="perpt">
			<span class="perpt-label">per pt</span>
			{#if yields.length === 0}
				<span class="yield empty" title="No derived-stat contribution yet">—</span>
			{:else}
				{#each yields as y (y.id)}
					<span class="yield">
						<span class="amt" style="color: {color}">+{formatNum(y.delta)}{derivedUnit(y.id)}</span>
						{derivedShortLabel(y.id)}
					</span>
				{/each}
			{/if}
		</div>
	</div>

	<div class="bar">
		<div class="bar-bg"></div>
		<div class="bar-saved" style="width: {savedPct}%"></div>
		{#if changed}
			<div class="bar-delta" class:down={value < saved} style="left: {deltaLeft}%; width: {deltaWidth}%"></div>
		{/if}
	</div>

	<div class="total">
		<span class="total-val" class:changed>{value}</span>
		{#if changed}
			<span class="total-delta" class:down={value < saved}>{value > saved ? '+' : ''}{value - saved}</span>
		{/if}
	</div>

	<Stepper canDec={view.canDec(i)} canInc={view.canInc()} onDec={() => view.dec(i)} onInc={() => view.inc(i)} />
</div>

<script lang="ts">
import { formatNum, attributeColor, attributeCode, attributeName } from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import { getAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import Stepper from './Stepper.svelte';
import {
	CORE_ATTRIBUTES,
	derivedShortLabel,
	derivedUnit,
	perPointYields,
	type AttributesView
} from './attributes-view.svelte';

interface Props {
	i: number;
	view: AttributesView;
}

const { i, view }: Props = $props();

const id = $derived(CORE_ATTRIBUTES[i]);
const color = $derived(attributeColor(id));
const code = $derived(attributeCode(id, staticData.attributes));
const name = $derived(attributeName(id, staticData.attributes));

// Hover explainer for the attribute, driven through the screen-level controller.
const attrTip = getAttributeTooltip();
const showTip = (e: MouseEvent) => attrTip?.show(id, e);
const moveTip = (e: MouseEvent) => attrTip?.move(e);
const hideTip = () => attrTip?.hide();

const value = $derived(view.values[i]);
const saved = $derived(view.savedValues[i]);
const changed = $derived(value !== saved);
const yields = $derived(perPointYields(i, view.values));

// Allocation bar segments are scaled against the same radar maximum so the bar
// and the radar agree visually.
const savedPct = $derived((Math.min(saved, value) / view.hexMax) * 100);
const deltaLeft = $derived((Math.min(saved, value) / view.hexMax) * 100);
const deltaWidth = $derived((Math.abs(value - saved) / view.hexMax) * 100);
</script>

<style lang="scss">
.trow {
	flex: 1;
	min-height: 0;
	display: grid;
	grid-template-columns: 40px 1.7fr 1.3fr 0.8fr auto;
	gap: 14px;
	align-items: center;
	padding: 0 16px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
}

.attr-hit {
	display: inline-flex;
	flex-shrink: 0;
}

.attr {
	min-width: 0;
}

.head {
	display: flex;
	align-items: center;
	gap: 8px;
}

.code {
	font-family: var(--mono);
	font-size: 11px;
}

.name {
	font-size: 13.5px;
	color: var(--text-primary);
}

.perpt {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-top: 6px;
	margin-left: 15px;
	flex-wrap: wrap;
}

.perpt-label {
	font-family: var(--mono);
	font-size: 7.5px;
	letter-spacing: 0.8px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 30%, transparent);
}

.yield {
	font-family: var(--mono);
	font-size: 9.5px;
	color: color-mix(in srgb, var(--text-primary) 42%, transparent);
	white-space: nowrap;

	&.empty {
		color: var(--text-muted);
	}
}

.amt {
	font-weight: 500;
}

.bar {
	position: relative;
	height: 8px;
}

.bar-bg {
	position: absolute;
	inset: 0;
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 6%, transparent);
}

.bar-saved {
	position: absolute;
	left: 0;
	top: 0;
	bottom: 0;
	border-radius: 4px;
	background: color-mix(in srgb, var(--text-primary) 26%, transparent);
}

.bar-delta {
	position: absolute;
	top: 0;
	bottom: 0;
	border-radius: 4px;
	background: var(--accent);
	box-shadow: 0 0 7px color-mix(in srgb, var(--accent) 53%, transparent);

	&.down {
		background: color-mix(in srgb, var(--enemy-accent) 60%, transparent);
		box-shadow: none;
	}
}

.total {
	text-align: right;
}

.total-val {
	font-family: var(--mono);
	font-size: 16px;
	font-weight: 500;
	color: var(--text-primary);

	&.changed {
		color: var(--accent);
	}
}

.total-delta {
	font-family: var(--mono);
	font-size: 9.5px;
	margin-left: 4px;
	color: color-mix(in srgb, var(--success) 85%, transparent);

	&.down {
		color: var(--enemy-accent);
	}
}
</style>
