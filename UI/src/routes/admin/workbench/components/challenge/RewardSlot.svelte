<!-- A single reward slot (item or mod): the current selection plus the open/change trigger. -->
<div class="ch-reward-slot" class:filled={valueId != null} class:open>
	<div class="ch-reward-head">
		<span class="ch-reward-ic" style={valueId != null && color ? `color:${color};border-color:${color}` : ''}>
			<WorkbenchIcon kind={kind === 'item' ? 'box' : kind === 'mod' ? 'rune' : 'bolt'} size={15} />
		</span>
		<div class="ch-reward-body">
			<div class="ch-reward-label">
				{label}{#if dirty}<span class="ch-reward-dot"></span>{/if}
			</div>
			{#if valueId != null}
				<div class="ch-reward-name" style={color ? `color:${color}` : ''}>
					{name}<span class="ch-reward-sub">{sub}</span>
				</div>
			{:else}
				<div class="ch-reward-empty">Nothing unlocked</div>
			{/if}
		</div>
		{#if valueId != null}
			<button type="button" class="row-x" title="Clear reward" onclick={onClear}>
				<WorkbenchIcon kind="x" size={11} />
			</button>
		{/if}
	</div>
	<button type="button" class="btn sm reward-cta" class:primary={valueId == null} onclick={onOpen}>
		<WorkbenchIcon kind={open ? 'chevD' : 'plus'} size={12} />{valueId != null ? 'Change…' : `Choose ${kind}…`}
	</button>
</div>

<script lang="ts">
import WorkbenchIcon from '../../WorkbenchIcon.svelte';

interface Props {
	kind: 'item' | 'mod' | 'skill';
	label: string;
	valueId: number | undefined;
	name: string | undefined;
	sub: string;
	color: string | null;
	dirty: boolean;
	open: boolean;
	onClear: () => void;
	onOpen: () => void;
}

const { kind, label, valueId, name, sub, color, dirty, open, onClear, onOpen }: Props = $props();
</script>

<style lang="scss">
.ch-reward-body {
	flex: 1;
	min-width: 0;
}
.reward-cta {
	margin-top: 12px;
}
.ch-reward-dot {
	display: inline-block;
	width: 5px;
	height: 5px;
	margin-left: 7px;
	border-radius: 50%;
	background: var(--warning);
	box-shadow: 0 0 5px var(--warning);
}
</style>
