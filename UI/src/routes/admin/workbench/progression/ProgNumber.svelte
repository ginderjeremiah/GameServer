<div class="numfld" style:width={width ? `${width}px` : undefined}>
	{#if label}<span class="lbl" class:warn>{label}</span>{/if}
	<NumInput class="pnum{warn ? ' invalid' : ''}" ariaLabel={label} {value} {allowNegative} {onChange} />
</div>

<script lang="ts">
import NumInput from '../components/NumInput.svelte';

interface Props {
	label?: string;
	value: number;
	onChange: (value: number) => void;
	width?: number;
	allowNegative?: boolean;
	warn?: boolean;
}

const { label, value, onChange, width, allowNegative = false, warn = false }: Props = $props();
</script>

<style lang="scss">
.numfld {
	display: flex;
	flex-direction: column;
	gap: 7px;
}
.lbl {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);

	&.warn {
		color: var(--warning);
	}
}
// NumInput renders its own <input> (a child component root), so target it globally but
// namespaced to this control's wrapper class to avoid touching other inputs.
:global(.numfld .pnum) {
	width: 100%;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-family: var(--mono);
	text-align: right;
	font-size: 13.5px;
	padding: 8px 11px;
}
:global(.numfld .pnum.invalid) {
	border-color: color-mix(in srgb, var(--warning) 55%, transparent);
}
:global(.numfld .pnum:focus) {
	border-color: var(--accent);
	outline: none;
	box-shadow: 0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent);
}
</style>
