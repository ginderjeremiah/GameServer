<div class="field-group">
	<input
		{type}
		{placeholder}
		bind:value
		data-testid={testid}
		{onblur}
		{onkeydown}
		{onkeyup}
		class="underline-input"
		class:error
		class:valid
	/>
	{#if trailing}
		<div class="field-trailing">
			{@render trailing()}
		</div>
	{/if}
	{#if below}
		{@render below()}
	{/if}
</div>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Two-way bound input value. */
	value: string;
	type?: 'text' | 'password';
	placeholder: string;
	testid: string;
	/** Applies the error accent to the underline. */
	error?: boolean;
	/** Applies the valid accent to the underline. */
	valid?: boolean;
	onblur?: (event: FocusEvent) => void;
	onkeydown?: (event: KeyboardEvent) => void;
	onkeyup?: (event: KeyboardEvent) => void;
	/** Trailing affordance pinned to the right edge (validation icon, reveal toggle, …). */
	trailing?: Snippet;
	/** Optional content rendered in-flow beneath the input (e.g. a strength meter). */
	below?: Snippet;
}

let {
	value = $bindable(),
	type = 'text',
	placeholder,
	testid,
	error = false,
	valid = false,
	onblur,
	onkeydown,
	onkeyup,
	trailing,
	below
}: Props = $props();
</script>

<style lang="scss">
.field-group {
	position: relative;
	margin-bottom: 22px;
}

.underline-input {
	width: 100%;
	height: 44px;
	padding: 0 36px 0 0;
	background: transparent;
	border: none;
	border-bottom: 1px solid color-mix(in srgb, var(--text-primary) 45%, transparent);
	outline: none;
	color: var(--text-primary);
	font-size: 18px;
	font-family: inherit;
	letter-spacing: 0.2px;
	transition: border-color 160ms;

	&.error {
		border-bottom-color: var(--error);
	}

	&.valid {
		border-bottom-color: var(--success);
	}
}

.field-trailing {
	position: absolute;
	right: 0;
	top: 22px;
	transform: translateY(-50%);
	display: flex;
}
</style>
