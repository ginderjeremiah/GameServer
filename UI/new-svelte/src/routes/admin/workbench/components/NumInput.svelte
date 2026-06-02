<input
	class={className}
	type="text"
	inputmode="decimal"
	value={text}
	oninput={handleInput}
	onfocus={() => (focused = true)}
	onblur={() => (focused = false)}
/>

<script lang="ts">
interface Props {
	value: number;
	onChange: (value: number) => void;
	class?: string;
	allowNegative?: boolean;
}

let { value, onChange, class: className = '', allowNegative = false }: Props = $props();

// Keep the in-progress text locally so typing "1." or "0.5" isn't clobbered by
// re-coercion on every keystroke, and reflect external resets when not editing.
const numToStr = (v: number) => (Number.isFinite(v) ? String(v) : '');
let inputText = $state<string>();
const text = $derived(inputText ?? numToStr(value));
let focused = $state(false);

$effect(() => {
	if (focused) {
		return;
	}

	const next = numToStr(value);
	if (next !== inputText) {
		inputText = next;
	}
});

const pattern = $derived(allowNegative ? /^-?\d*\.?\d*$/ : /^\d*\.?\d*$/);

const handleInput = (event: Event) => {
	const el = event.currentTarget as HTMLInputElement;
	const raw = el.value;
	if (raw !== '' && !pattern.test(raw)) {
		el.value = text; // reject the keystroke
		return;
	}
	inputText = raw;
	if (raw === '' || raw === '-' || raw === '.' || raw === '-.') {
		onChange(0);
		return;
	}
	const parsed = parseFloat(raw);
	if (!Number.isNaN(parsed)) {
		onChange(parsed);
	}
};
</script>
