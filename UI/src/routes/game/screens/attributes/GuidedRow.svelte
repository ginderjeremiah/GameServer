<div class="row">
	<span class="bullet" style="--c: {color}"></span>

	<div class="info">
		<div class="head">
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
	</div>

	<Delta from={view.savedValues[i]} to={view.values[i]} />
	<Stepper canDec={view.canDec(i)} canInc={view.canInc()} onDec={() => view.dec(i)} onInc={() => view.inc(i)} />
</div>

<script lang="ts">
import { attributeColor, attributeCode, attributeName } from '$lib/common';
import { staticData } from '$stores';
import Delta from './Delta.svelte';
import Stepper from './Stepper.svelte';
import { CORE_ATTRIBUTES, DERIVED_STATS, feedsFor, type AttributesView } from './attributes-view.svelte';

interface Props {
	i: number;
	view: AttributesView;
}

const { i, view }: Props = $props();

const id = $derived(CORE_ATTRIBUTES[i]);
const color = $derived(attributeColor(id));
const code = $derived(attributeCode(id));
const name = $derived(attributeName(id, staticData.attributes));
const feeds = $derived(feedsFor(i));

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

.bullet {
	width: 8px;
	height: 8px;
	border-radius: 50%;
	flex-shrink: 0;
	background: var(--c);
	box-shadow: 0 0 7px color-mix(in srgb, var(--c) 60%, transparent);
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
