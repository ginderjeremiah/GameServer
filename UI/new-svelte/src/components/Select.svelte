{#if label}
	<label class="select-label" for={id}> {label} </label>
{/if}
<div class="select-wrapper round-border hover-glow">
	<div class="overflow-boundary round-border">
		<select {id} bind:value>
			{#each selectOptions as option (option.value)}
				<option value={option.value} selected={option.selected}>{option.text}</option>
			{/each}
		</select>
	</div>
</div>

<script lang="ts" module>
export interface SelectOptions {
	disableBlanks?: boolean;
	options: { id: number; name: string }[];
}
</script>

<script lang="ts">
import { untrack } from 'svelte';

interface Props extends SelectOptions {
	value: number;
	label?: string;
	id?: string;
}

interface OptionData {
	text: string;
	value: number;
	selected: boolean;
}

let {
	value = $bindable(),
	label,
	id = crypto.randomUUID(),
	disableBlanks,
	options
}: Props = $props();

let selectOptions = $state<OptionData[]>([]);
let selected = $state<OptionData>();

const allowBlanks = $derived(!disableBlanks);

$effect(() => {
	const opts = options.map((o) => ({
		text: o.name,
		value: o.id,
		selected: false
	}));

	if (allowBlanks) {
		opts.unshift({
			text: '',
			value: -1,
			selected: false
		});
	}

	selectOptions = opts;
	selected = undefined;
});

$effect(() => {
	if (value !== untrack(() => selected?.value)) {
		untrack(() => {
			if (selected) {
				selected.selected = false;
			}
		});

		let newSelected = selectOptions.find((o) => o.value === value);
		if (newSelected) {
			newSelected.selected = true;
			selected = newSelected;
		} else if (allowBlanks) {
			value = -1;
			newSelected = selectOptions?.[0];
			if (newSelected) {
				newSelected.selected = true;
				selected = newSelected;
			}
		}
	}
});
</script>

<style lang="scss">
.select-label {
	display: block;
	padding-bottom: 0.25rem;
	font-size: 0.8rem;
}

.select-wrapper {
	box-sizing: border-box;
	height: 1.5rem;
	width: 100%;
	border: var(--default-border);
	cursor: text;
	pointer-events: all;
	user-select: text;

	.overflow-boundary {
		height: 100%;
		overflow: hidden;
		position: relative;
	}

	select {
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
