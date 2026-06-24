<input
	class={className}
	type="text"
	inputmode="decimal"
	aria-label={ariaLabel}
	{style}
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
	style?: string;
	allowNegative?: boolean;
	/** Accessible name for the input — the visible field labels are presentational `<span>`s, not associated `<label>`s. */
	ariaLabel?: string;
}

let {
	value,
	onChange,
	class: className = '',
	style = undefined,
	allowNegative = false,
	ariaLabel = undefined
}: Props = $props();

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
	// Only commit a fully-parseable number. An empty or in-progress field ("", "-", ".", "-.")
	// leaves the stored value untouched rather than coercing it to 0 — navigating away mid-edit
	// then restores the prior value (via the blur effect) instead of silently persisting 0.
	const parsed = parseFloat(raw);
	if (!Number.isNaN(parsed)) {
		onChange(parsed);
	}
};
</script>
