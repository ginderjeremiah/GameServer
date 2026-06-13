<!-- One selectable attribute in the left rail: its name, a full-width mini stack
     bar showing its source composition, and its total. -->
<button
	type="button"
	class="row"
	class:active
	data-testid="attr-row-{meta.id}"
	aria-pressed={active}
	onclick={() => onSelect(meta.id)}
>
	<span class="info">
		<span class="name">{attributeName(meta.id, staticData.attributes)}</span>
		<StackBar {computed} height={6} radius={1} />
	</span>
	<span class="total">{fmtNum(computed.total, meta.dec)}</span>
</button>

<script lang="ts">
import type { ComputedAttribute } from '$lib/battle';
import { attributeName } from '$lib/common';
import { staticData } from '$stores';
import StackBar from './StackBar.svelte';
import { fmtNum, type BreakdownAttrMeta, type LabeledModifier } from './attribute-breakdown-view.svelte';

interface Props {
	meta: BreakdownAttrMeta;
	computed: ComputedAttribute<LabeledModifier>;
	active: boolean;
	onSelect: (id: number) => void;
}

let { meta, computed, active, onSelect }: Props = $props();
</script>

<style lang="scss">
.row {
	width: 100%;
	text-align: left;
	display: grid;
	grid-template-columns: 1fr 54px;
	align-items: center;
	gap: 8px;
	padding: 7px 9px;
	margin-bottom: 2px;
	border-radius: 4px;
	cursor: pointer;
	background: transparent;
	border: 1px solid transparent;
	transition: background 120ms;

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 10%, transparent);
		border-color: color-mix(in srgb, var(--accent) 40%, transparent);
	}
}

.info {
	min-width: 0;
}

.name {
	display: block;
	font-size: 12.5px;
	color: var(--text-secondary);
	margin-bottom: 4px;
	white-space: nowrap;

	.active & {
		color: var(--text-primary);
	}
}

.total {
	font-family: var(--mono);
	font-size: 12px;
	text-align: right;
	color: var(--text-tertiary);

	.active & {
		color: var(--text-primary);
	}
}
</style>
