<div class="row">
	<!-- The icon is the row's keyboard-reachable tooltip trigger (the name below stays mouse-only to
	     avoid a redundant second tab stop). svelte-ignore: a labelled role="img" is intentionally focusable. -->
	<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
	<span
		class="attr-hit"
		role="img"
		tabindex="0"
		aria-label={name}
		use:tooltipHover={{ controller: attrTip, payload: id }}
		use:describedByTooltip={attrTip?.describedById}
	>
		<AttributeIcon {id} size={40} />
	</span>

	<div class="info">
		<div class="head" use:tooltipHover={{ controller: attrTip, payload: id }}>
			<span class="code" style="color: {color}">{code}</span>
			<span class="name">{name}</span>
		</div>
		<div class="feeds">
			<span class="feeds-label">Feeds</span>
			{#if feeds.length === 0}
				<span class="feed empty" title="No derived-stat contribution yet">—</span>
			{:else}
				{#each feeds as fid (fid)}
					<span class="feed" class:changed={changed.has(fid)}>{attributeName(fid, staticData.attributes)}</span>
				{/each}
			{/if}
		</div>
		{#if inert}
			<DormantNote text={hint ?? ''} block marginTop={4} />
		{/if}
	</div>

	<Delta from={view.savedValues[i]} to={view.values[i]} />
	<Stepper canDec={view.canDec(i)} canInc={view.canInc()} onDec={() => view.dec(i)} onInc={() => view.inc(i)} />
</div>

<script lang="ts">
import { attributeColor, attributeCode, attributeName } from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import DormantNote from '$components/DormantNote.svelte';
import { getAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import { tooltipHover } from '$components/tooltip/tooltip-hover';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import Delta from './Delta.svelte';
import Stepper from './Stepper.svelte';
import { CORE_ATTRIBUTES, DERIVED_STATS, feedsFor, inertHint, type AttributesView } from './attributes-view.svelte';

interface Props {
	i: number;
	view: AttributesView;
}

const { i, view }: Props = $props();

const id = $derived(CORE_ATTRIBUTES[i]);
const color = $derived(attributeColor(id));
const code = $derived(attributeCode(id, staticData.attributes));
const name = $derived(attributeName(id, staticData.attributes));
const feeds = $derived(feedsFor(i));
// Read-only "dormant, not dead" signal for the amplifier attributes (AGI/LUK, spike #1426 Decision 5) —
// no matching enabler is fielded, so the current build gets nothing from points spent here.
const inert = $derived(view.isInert(i));
const hint = $derived(inertHint(id));

// Hover explainer for the attribute, driven through the screen-level controller.
const attrTip = getAttributeTooltip();

// The set of derived stats whose value differs between the saved and pending
// builds — used to light up the feed chips this attribute drives.
const changed = $derived(
	new Set(DERIVED_STATS.filter((d) => view.savedDerived[d.id] !== view.derived[d.id]).map((d) => d.id))
);
</script>

<style lang="scss">
.row {
	display: flex;
	align-items: center;
	gap: 14px;
	padding: 9px 13px;
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 2%, transparent);
	border: 1px solid var(--border-subtle);
}

.attr-hit {
	display: inline-flex;
	flex-shrink: 0;

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
		border-radius: 4px;
	}
}

.info {
	flex: 1;
	min-width: 0;
}

.head {
	display: flex;
	align-items: baseline;
	gap: 8px;
}

.code {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
}

.name {
	font-size: 13.5px;
	color: var(--text-primary);
}

.feeds {
	display: flex;
	align-items: center;
	gap: 6px;
	margin-top: 4px;
	flex-wrap: wrap;
}

.feeds-label {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 32%, transparent);
}

.feed {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.2px;
	padding: 1px 6px;
	border-radius: 2px;
	white-space: nowrap;
	color: var(--text-tertiary);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 9%, transparent);
	transition: all 160ms;

	&.changed {
		color: var(--accent-light);
		background: color-mix(in srgb, var(--accent) 10%, transparent);
		border-color: color-mix(in srgb, var(--accent) 33%, transparent);
	}

	&.empty {
		color: var(--text-muted);
		border-style: dashed;
	}
}
</style>
