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
			<svg class="select-caret" width="9" height="9" viewBox="0 0 10 10" fill="none" stroke="rgba(240,240,240,0.5)" stroke-width="1.4" aria-hidden="true">
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
					<svg width="11" height="11" viewBox="0 0 12 12" fill="none" stroke="#c0d8ff" stroke-width="1.8" aria-hidden="true">
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
	border-bottom: 1px solid rgba(255, 255, 255, 0.05);
	vertical-align: middle;
}

.id-cell {
	font-family: 'Geist Mono', monospace;
	font-size: 12px;
	letter-spacing: 0.5px;
	color: rgba(240, 240, 240, 0.45);

	&.is-new {
		color: var(--accent);
	}
}

.readonly-cell {
	font-size: 13px;
	color: rgba(240, 240, 240, 0.5);
}

.control-wrap {
	position: relative;
	width: 100%;
}

// Cap each editor by data type so columns stay only as wide as their content
// needs and the table doesn't sprawl across the page.
.num-wrap {
	max-width: 90px;
}

.select-wrap {
	max-width: 200px;
}

.str-wrap {
  width: 100%;
  min-width: 260px;
}

.adm-input {
  field-sizing: content;
	width: 100%;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.1);
	border-radius: 3px;
	color: #f0f0f0;
	font-family: Geist, sans-serif;
	font-size: 13px;
	padding: 6px 8px;
	outline: none;
	transition: border-color 140ms ease, box-shadow 140ms ease, background 140ms ease;

	&.num {
		font-family: 'Geist Mono', monospace;
		text-align: right;
	}

	&:hover:not(:disabled) {
		border-color: rgba(255, 255, 255, 0.2);
	}

	&:focus {
		border-color: var(--accent);
		box-shadow: 0 0 0 1px rgba(161, 194, 247, 0.4), 0 0 8px rgba(161, 194, 247, 0.35);
		background: rgba(161, 194, 247, 0.04);
	}

	&:disabled {
		color: rgba(240, 240, 240, 0.5);
		cursor: default;
	}

	&.dirty {
		border-color: rgba(240, 210, 138, 0.55);
		background: rgba(240, 210, 138, 0.06);
	}

	&.dirty:focus {
		border-color: var(--accent);
		background: rgba(161, 194, 247, 0.04);
	}
}

.adm-select {
	width: 100%;
	appearance: none;
	-webkit-appearance: none;
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.1);
	border-radius: 3px;
	color: #f0f0f0;
	font-family: Geist, sans-serif;
	font-size: 13px;
	padding: 6px 26px 6px 8px;
	outline: none;
	cursor: pointer;
	transition: border-color 140ms ease, box-shadow 140ms ease, background 140ms ease;

	&:hover:not(:disabled) {
		border-color: rgba(255, 255, 255, 0.2);
	}

	&:focus {
		border-color: var(--accent);
		box-shadow: 0 0 0 1px rgba(161, 194, 247, 0.4), 0 0 8px rgba(161, 194, 247, 0.35);
	}

	&:disabled {
		color: rgba(240, 240, 240, 0.5);
		cursor: default;
	}

	&.dirty {
		border-color: rgba(240, 210, 138, 0.55);
		background: rgba(240, 210, 138, 0.06);
	}

	&.dirty:focus {
		border-color: var(--accent);
		background: rgba(161, 194, 247, 0.04);
	}

	option {
		background: #14151b;
		color: #f0f0f0;
	}
}

.select-caret {
	position: absolute;
	right: 9px;
	top: 50%;
	transform: translateY(-50%);
	pointer-events: none;
}

.bool-wrap {
	display: flex;
	justify-content: center;
}

.bool-toggle {
	width: 18px;
	height: 18px;
	border-radius: 3px;
	border: 1px solid rgba(255, 255, 255, 0.22);
	background: transparent;
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	padding: 0;
	transition: all 140ms ease;

	&.on {
		border-color: var(--accent);
		background: rgba(161, 194, 247, 0.18);
	}

	&.dirty {
		border-color: var(--warning);
		background: rgba(240, 210, 138, 0.06);
	}

	&.on.dirty {
		border-color: var(--warning);
		background: rgba(240, 210, 138, 0.12);
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
	background: var(--warning);
	box-shadow: 0 0 6px rgba(240, 210, 138, 0.9);
	pointer-events: none;
}
</style>
