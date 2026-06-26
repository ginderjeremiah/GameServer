<div class="selfld" style:width={width ? `${width}px` : undefined}>
	{#if label}<span class="lbl" class:warn>{label}</span>{/if}
	<div class="sel-wrap">
		<select
			class="sel"
			class:invalid={warn}
			aria-label={ariaLabel ?? label}
			{value}
			onchange={(e) => onChange(+e.currentTarget.value)}
		>
			{#each options as option (option.value)}
				<option value={option.value}>{option.text}</option>
			{/each}
		</select>
		<SelectCaret />
	</div>
</div>

<script lang="ts">
import SelectCaret from '../components/SelectCaret.svelte';
import type { SelectOption } from '../entities/types';

interface Props {
	label?: string;
	value: number;
	options: SelectOption[];
	onChange: (value: number) => void;
	width?: number;
	warn?: boolean;
	/** Accessible name when the control has no visible `label` (e.g. table-row selects). */
	ariaLabel?: string;
}

const { label, value, options, onChange, width, warn = false, ariaLabel }: Props = $props();
</script>

<style lang="scss">
.selfld {
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
.sel-wrap {
	position: relative;
}
.sel {
	width: 100%;
	appearance: none;
	-webkit-appearance: none;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-size: 13.5px;
	padding: 8px 28px 8px 11px;
	cursor: pointer;

	&.invalid {
		border-color: color-mix(in srgb, var(--warning) 55%, transparent);
	}
	&:focus {
		border-color: var(--accent);
		outline: none;
		box-shadow: 0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent);
	}
}
</style>
