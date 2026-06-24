<!-- A single reward slot (item or mod): the current selection plus the open/change trigger. -->
<div class="ch-reward-slot" class:filled={valueId != null} class:open>
	<div class="ch-reward-head">
		<span class="ch-reward-ic" style={valueId != null && color ? `color:${color};border-color:${color}` : ''}>
			<WorkbenchIcon kind={kind === 'item' ? 'box' : 'rune'} size={15} />
		</span>
		<div class="ch-reward-body">
			<div class="ch-reward-label">
				{label}{#if dirty}<span class="ch-reward-dot"></span>{/if}
			</div>
			{#if valueId != null}
				<div class="ch-reward-name" class:retired style={color ? `color:${color}` : ''}>
					{name}<span class="ch-reward-sub">{sub}</span>
					{#if retired}<span class="ch-reward-retired">retired</span>{/if}
				</div>
			{:else}
				<div class="ch-reward-empty">Nothing unlocked</div>
			{/if}
		</div>
		{#if valueId != null}
			<button type="button" class="row-x" title="Clear reward" aria-label="Clear reward" onclick={onClear}>
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
	kind: 'item' | 'mod';
	label: string;
	valueId: number | undefined;
	name: string | undefined;
	sub: string;
	color: string | null;
	/** The current reward references a retired record — flagged so the author knows it's out of circulation. */
	retired?: boolean;
	dirty: boolean;
	open: boolean;
	onClear: () => void;
	onOpen: () => void;
}

const { kind, label, valueId, name, sub, color, retired = false, dirty, open, onClear, onOpen }: Props = $props();
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
.ch-reward-name.retired {
	text-decoration: line-through;
	text-decoration-color: var(--text-muted);
}
.ch-reward-retired {
	margin-left: 8px;
	font-family: var(--mono);
	font-size: 9.5px;
	text-transform: uppercase;
	letter-spacing: 0.05em;
	color: var(--text-muted);
	border: 1px solid color-mix(in srgb, var(--text-muted) 45%, transparent);
	border-radius: 3px;
	padding: 1px 5px;
	text-decoration: none;
	vertical-align: middle;
}
</style>
