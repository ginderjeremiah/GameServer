<td class="cell">
	{#if noEditor}
		<span class="id-cell" class:is-new={isNew}>{isNew ? 'new' : data}</span>
	{:else if selectOptions}
		<div class="control-wrap select-wrap">
			<select class="adm-select" class:dirty {disabled} bind:value={data}>
				{#each options as option (option.value)}
					<option value={option.value}>{option.text}</option>
				{/each}
			</select>
			<svg
				class="select-caret"
				width="9"
				height="9"
				viewBox="0 0 10 10"
				fill="none"
				stroke="currentColor"
				stroke-width="1.4"
				aria-hidden="true"
			>
				<path d="M2 3.5L5 6.5L8 3.5" stroke-linecap="round" stroke-linejoin="round" />
			</svg>
			{#if dirty}<span class="dirty-dot" title="Changed from saved value"></span>{/if}
		</div>
	{:else if isBool}
		<div class="control-wrap bool-wrap">
			<button
				type="button"
				class="bool-toggle"
				class:on={!!data}
				class:dirty
				{disabled}
				aria-pressed={!!data}
				onclick={toggleBool}
			>
				{#if data}
					<svg
						width="11"
						height="11"
						viewBox="0 0 12 12"
						fill="none"
						stroke="currentColor"
						stroke-width="1.8"
						aria-hidden="true"
					>
						<path d="M2.5 6.2L4.8 8.5L9.5 3.5" stroke-linecap="round" stroke-linejoin="round" />
					</svg>
				{/if}
			</button>
		</div>
	{:else if isNumber}
		<div class="control-wrap num-wrap">
			<input
				class="adm-input num"
				class:dirty
				type="text"
				inputmode="decimal"
				value={numText}
				{disabled}
				oninput={onNumberInput}
				onfocus={() => (focused = true)}
				onblur={() => (focused = false)}
			/>
			{#if dirty}<span class="dirty-dot" title="Changed from saved value"></span>{/if}
		</div>
	{:else if isString}
		<div class="control-wrap str-wrap">
			<input class="adm-input" class:dirty type="text" {disabled} bind:value={data} />
			{#if dirty}<span class="dirty-dot" title="Changed from saved value"></span>{/if}
		</div>
	{:else}
		<span class="readonly-cell">{data?.toString()}</span>
	{/if}
</td>

<script lang="ts" generics="T">
import type { SelectOptions } from '$components';

interface Props {
	data: T;
	disabled?: boolean;
	selectOptions?: SelectOptions;
	noEditor?: boolean;
	dirty?: boolean;
}

let { data = $bindable(), disabled, selectOptions, noEditor, dirty }: Props = $props();

// Capture the editor kind once — number fields that get cleared mid-edit must
// keep behaving like number fields.
const dataType = typeof data;
const isString = dataType === 'string';
const isBool = dataType === 'boolean';
const isNumber = dataType === 'number';
const isNew = $derived(noEditor === true && isNumber && (data as unknown as number) < 0);

const options = $derived.by(() => {
	if (!selectOptions) return [];
	const opts = selectOptions.options.map((o) => ({ value: o.id, text: o.name }));
	if (!selectOptions.disableBlanks) {
		opts.unshift({ value: -1, text: '' });
	}
	return opts;
});

const toggleBool = () => {
	if (!disabled) data = !data as unknown as T;
};

// ── Numeric input: reject anything that isn't a valid number (digits, an
// optional leading minus, and a single decimal point). Empty is allowed. ──
const numToStr = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? String(v) : '');
let focused = $state(false);
let numText = $state(isNumber ? numToStr(data) : '');

// Reflect external data changes (Reset / Discard / Save) into the buffer, but
// never while the user is actively typing in this field.
$effect(() => {
	if (!isNumber || focused) return;
	const s = numToStr(data);
	if (s !== numText) numText = s;
});

const onNumberInput = (e: Event) => {
	const el = e.currentTarget as HTMLInputElement;
	const v = el.value;
	if (v !== '' && !/^-?\d*\.?\d*$/.test(v)) {
		el.value = numText; // reject the keystroke
		return;
	}
	numText = v;
	if (v === '' || v === '-' || v === '.' || v === '-.') {
		data = 0 as unknown as T;
		return;
	}
	const n = Number(v);
	if (Number.isFinite(n)) data = n as unknown as T;
};
</script>

<style lang="scss">
.cell {
	padding: 6px 10px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 5%, transparent);
	vertical-align: middle;
}

.id-cell {
	font-family: var(--mono);
	font-size: 12px;
	letter-spacing: 0.5px;
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);

	&.is-new {
		color: var(--accent);
	}
}

.readonly-cell {
	font-size: 13px;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
}

.control-wrap {
	position: relative;
	width: 100%;
}

// Cap each editor by data type so columns stay only as wide as their content
// needs and the table doesn't sprawl across the page.
.num-wrap {
	width: 100%;
	min-width: 90px;
}

.select-wrap {
	width: 100%;
	min-width: 200px;
}

.str-wrap {
	width: 100%;
	min-width: 260px;
}

.adm-input {
	field-sizing: content;
	width: 100%;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-family: Geist, sans-serif;
	font-size: 13px;
	padding: 6px 8px;
	outline: none;
	transition:
		border-color 140ms ease,
		box-shadow 140ms ease,
		background 140ms ease;

	&.num {
		font-family: var(--mono);
		text-align: right;
	}

	&:hover:not(:disabled) {
		border-color: color-mix(in srgb, var(--white) 20%, transparent);
	}

	&:focus {
		border-color: var(--accent);
		box-shadow:
			0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent),
			0 0 8px color-mix(in srgb, var(--accent) 35%, transparent);
		background: color-mix(in srgb, var(--accent) 4%, transparent);
	}

	&:disabled {
		color: color-mix(in srgb, var(--text-primary) 50%, transparent);
		cursor: default;
	}

	&.dirty {
		border-color: color-mix(in srgb, var(--change-modified) 55%, transparent);
		background: color-mix(in srgb, var(--change-modified) 6%, transparent);
	}

	&.dirty:focus {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 4%, transparent);
	}
}

.adm-select {
	width: 100%;
	appearance: none;
	-webkit-appearance: none;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid color-mix(in srgb, var(--white) 10%, transparent);
	border-radius: 3px;
	color: var(--text-primary);
	font-family: Geist, sans-serif;
	font-size: 13px;
	padding: 6px 26px 6px 8px;
	outline: none;
	cursor: pointer;
	transition:
		border-color 140ms ease,
		box-shadow 140ms ease,
		background 140ms ease;

	&:hover:not(:disabled) {
		border-color: color-mix(in srgb, var(--white) 20%, transparent);
	}

	&:focus {
		border-color: var(--accent);
		box-shadow:
			0 0 0 1px color-mix(in srgb, var(--accent) 40%, transparent),
			0 0 8px color-mix(in srgb, var(--accent) 35%, transparent);
	}

	&:disabled {
		color: color-mix(in srgb, var(--text-primary) 50%, transparent);
		cursor: default;
	}

	&.dirty {
		border-color: color-mix(in srgb, var(--change-modified) 55%, transparent);
		background: color-mix(in srgb, var(--change-modified) 6%, transparent);
	}

	&.dirty:focus {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 4%, transparent);
	}

	option {
		background: var(--surface);
		color: var(--text-primary);
	}
}

.select-caret {
	position: absolute;
	right: 9px;
	top: 50%;
	transform: translateY(-50%);
	pointer-events: none;
	// `stroke="currentColor"` on the markup resolves to this colour, keeping the
	// caret theme-overridable (CSS custom properties can't be used in the SVG
	// presentation attribute directly).
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
}

.bool-wrap {
	display: flex;
	justify-content: center;
}

.bool-toggle {
	width: 18px;
	height: 18px;
	border-radius: 3px;
	border: 1px solid color-mix(in srgb, var(--white) 22%, transparent);
	background: transparent;
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 0;
	transition: all 140ms ease;

	&.on {
		border-color: var(--accent);
		background: color-mix(in srgb, var(--accent) 18%, transparent);
		// Drives the checkmark SVG's `stroke="currentColor"`.
		color: var(--accent-light);
	}

	&.dirty {
		border-color: var(--change-modified);
		background: color-mix(in srgb, var(--change-modified) 6%, transparent);
	}

	&.on.dirty {
		border-color: var(--change-modified);
		background: color-mix(in srgb, var(--change-modified) 12%, transparent);
	}

	&:disabled {
		cursor: default;
	}
}

.dirty-dot {
	position: absolute;
	top: -3px;
	right: -3px;
	width: 6px;
	height: 6px;
	border-radius: 50%;
	background: var(--change-modified);
	box-shadow: 0 0 6px color-mix(in srgb, var(--change-modified) 90%, transparent);
	pointer-events: none;
}
</style>
