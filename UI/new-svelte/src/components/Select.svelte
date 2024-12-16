<div class="select-wrapper round-border hover-glow">
	<div class="overflow-boundary round-border">
		<select bind:value>
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
interface Props extends SelectOptions {
	value: number;
}

interface OptionData {
	text: string;
	value: number;
	selected: boolean;
}

let { value = $bindable(), disableBlanks, options }: Props = $props();

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
	if (value !== selected?.value) {
		if (selected) {
			selected.selected = false;
		}

		const newSelected = selectOptions.find((o) => o.value === value);
		if (newSelected) {
			selected = newSelected;
			newSelected.selected = true;
		} else if (allowBlanks) {
			value = -1;
			selected = selectOptions?.[0];
			if (selected) {
				selected.selected = true;
			}
		}
	}
});
</script>

<style lang="scss">
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
