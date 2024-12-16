<td>
	{#if noEditor}
		{data}
	{:else if isNumber(data)}
		{#if selectOptions}
			<Select bind:value={data} {...selectOptions} />
		{:else}
			<TextInput bind:value={data} type={'number'} {disabled} />
		{/if}
	{:else if isBool(data)}
		<CheckBox bind:value={data} {disabled} />
	{:else if isString(data)}
		<TextInput bind:value={data} type={'text'} {disabled} />
	{:else}
		<TextInput value={data?.toString()} type={'text'} disabled />
	{/if}
</td>

<script lang="ts" generics="T">
import { CheckBox, TextInput, Select, type SelectOptions } from '$components';

interface Props {
	data: T;
	disabled?: boolean;
	selectOptions?: SelectOptions;
	noEditor?: boolean;
}

let { data = $bindable(), disabled, selectOptions, noEditor }: Props = $props();

const dataType = $derived(typeof data);

const isString = (data: T | string): data is string => dataType === 'string';
const isBool = (data: T | boolean): data is boolean => dataType === 'boolean';
const isNumber = (data: T | number): data is number => dataType === 'number';
</script>

<style lang="scss">
td {
	text-align: center;
	padding: 0.125em;
	border: var(--default-border);
}
</style>
