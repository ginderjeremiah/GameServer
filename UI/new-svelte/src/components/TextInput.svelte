{#if label}
	<label class="text-input-label" for={id}> {label} </label>
{/if}
<div class="text-input-wrapper round-border hover-glow">
	{#if type === 'text'}
		<input {id} class="round-border" bind:value {type} {name} />
	{:else if type === 'password'}
		<input {id} class="round-border" bind:value {type} {name} />
	{:else}
		<input {id} class="round-border" bind:value {type} {name} />
	{/if}
</div>

<script lang="ts" generics="T extends 'text' | 'password' | 'number'">
type Props = {
	value: (T extends 'number' ? number : string) | undefined;
	type: T;
	label?: string;
	name?: string;
	id?: string;
};

let {
	value = $bindable(),
	type,
	label = '',
	name = '',
	id = crypto.randomUUID()
}: Props = $props();
</script>

<style lang="scss">
.text-input-label {
	display: block;
	padding-bottom: 0.25rem;
	font-size: 0.8rem;
}

.text-input-wrapper {
	box-sizing: border-box;
	height: 1.5rem;
	width: 100%;
	border: 1px solid black;
	position: relative;
	cursor: text;
	pointer-events: all;
	user-select: text;

	input {
		position: absolute;
		top: 0;
		left: 0;
		z-index: 6;
		padding: 0.375rem 0.25rem;
		box-sizing: border-box;
		outline: none;
		border: none;
		height: 100%;
		width: 100%;
	}
}
</style>
