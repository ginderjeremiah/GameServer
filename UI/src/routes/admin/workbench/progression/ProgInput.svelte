<div class="fld" class:grow style:width={fullWidth ? '100%' : undefined}>
	{#if label}<span class="lbl" class:warn>{label}</span>{/if}
	{#if textarea}
		<textarea
			class="inp area"
			class:invalid={warn}
			aria-label={label}
			{placeholder}
			maxlength={maxLength}
			{value}
			oninput={(e) => onChange(e.currentTarget.value)}></textarea>
	{:else}
		<input
			class="inp"
			class:mono
			class:invalid={warn}
			aria-label={label}
			{placeholder}
			maxlength={maxLength}
			{value}
			oninput={(e) => onChange(e.currentTarget.value)}
		/>
	{/if}
</div>

<script lang="ts">
interface Props {
	label?: string;
	value: string;
	onChange: (value: string) => void;
	placeholder?: string;
	textarea?: boolean;
	grow?: boolean;
	fullWidth?: boolean;
	mono?: boolean;
	warn?: boolean;
	maxLength?: number;
}

const {
	label,
	value,
	onChange,
	placeholder = '',
	textarea = false,
	grow = false,
	fullWidth = false,
	mono = false,
	warn = false,
	maxLength
}: Props = $props();
</script>

<style lang="scss">
.fld {
	display: flex;
	flex-direction: column;
	gap: 7px;

	&.grow {
		flex: 1;
	}
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
.inp {
	width: 100%;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-family: var(--sans);
	font-size: 13.5px;
	padding: 8px 11px;

	&.mono {
		font-family: var(--mono);
	}
	&.invalid {
		border-color: color-mix(in srgb, var(--warning) 55%, transparent);
	}
	&:focus {
		border-color: var(--accent);
		outline: none;
		box-shadow: 0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent);
	}
}
.area {
	min-height: 84px;
	line-height: 1.5;
	resize: vertical;
}
</style>
