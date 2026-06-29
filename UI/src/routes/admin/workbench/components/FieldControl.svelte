{#if field.type === 'toggle'}
	<div class="fld">
		{@render label()}
		<button
			type="button"
			class="toggle"
			class:on={!!value}
			class:dirty
			role="switch"
			aria-checked={!!value}
			aria-label={field.label}
			onclick={() => set(!value)}
		>
			<span class="track"><span class="knob"></span></span>
			<span class="tg-label">{value ? field.onLabel : field.offLabel}</span>
		</button>
	</div>
{:else if field.type === 'textarea'}
	<div class="fld" style:flex={field.grow ? '1 1 100%' : 'none'} style:width="100%">
		{@render label()}
		<textarea
			class="txtarea"
			class:dirty
			class:invalid={!!warn}
			aria-label={field.label}
			placeholder={field.placeholder}
			value={value as string}
			oninput={(e) => set(e.currentTarget.value)}
		></textarea>
		{#if dirty}<DirtyDot />{/if}
	</div>
{:else if field.type === 'number'}
	<div class="fld" style:width="{field.width ?? 150}px">
		{@render label()}
		<div class="num-unit">
			<NumInput
				class="inp num{dirty ? ' dirty' : ''}{warn ? ' invalid' : ''}"
				ariaLabel={field.label}
				value={(value as number) ?? 0}
				allowNegative={field.allowNegative}
				onChange={(n) => set(n)}
			/>
			{#if field.suffix}<span class="suffix">{field.suffix}</span>{/if}
		</div>
		{#if dirty}<DirtyDot />{/if}
	</div>
{:else if field.type === 'flags'}
	<div class="fld" style:flex="1 1 100%" style:width="100%">
		{@render label()}
		<div class="flags">
			{#each field.flags ?? [] as flag (flag.value)}
				<button
					type="button"
					class="flag-chip"
					class:on={hasFlag((value as number) ?? 0, flag.value)}
					class:dirty
					aria-pressed={hasFlag((value as number) ?? 0, flag.value)}
					onclick={() =>
						set(toggleFlag((value as number) ?? 0, flag.value, !hasFlag((value as number) ?? 0, flag.value)))}
				>
					<span class="flag-box"></span>
					<span>{flag.label}</span>
				</button>
			{/each}
			{#if dirty}<DirtyDot />{/if}
		</div>
	</div>
{:else if field.type === 'select'}
	<div class="fld" style:width="{field.width ?? 170}px">
		{@render label()}
		<div style:position="relative">
			<select
				class="sel"
				class:dirty
				class:invalid={!!warn}
				aria-label={field.label}
				value={value as number}
				onchange={(e) => set(+e.currentTarget.value)}
			>
				{#each field.options?.(value as number) ?? [] as option (option.value)}
					<option value={option.value}>{option.text}</option>
				{/each}
			</select>
			<SelectCaret />
			{#if dirty}<DirtyDot />{/if}
		</div>
	</div>
{:else if field.type === 'attribute'}
	<div class="attr-fld" style:width="{field.width ?? 170}px">
		{@render label()}
		<AttributePicker
			value={value as number}
			options={field.options?.(value as number) ?? []}
			onChange={(v) => set(v)}
			ariaLabel={field.label}
			{dirty}
			invalid={!!warn}
		/>
	</div>
{:else}
	<div class="fld" style:flex={field.grow ? '1 1 280px' : 'none'} style:min-width={field.grow ? '240px' : 'auto'}>
		{@render label()}
		<input
			class="inp"
			class:dirty
			class:invalid={!!warn}
			aria-label={field.label}
			placeholder={field.placeholder}
			value={value as string}
			oninput={(e) => set(e.currentTarget.value)}
		/>
		{#if dirty}<DirtyDot />{/if}
	</div>
{/if}

{#snippet label()}
	<span class="lbl" class:warn={!!warn}>
		{field.label}
		{#if warn}<WorkbenchIcon kind="warn" size={11} sw={1.6} stroke="var(--warning)" />{/if}
	</span>
{/snippet}

<script lang="ts">
import { hasFlag, toggleFlag } from '$lib/common';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import type { EntityStore } from '../entity-store.svelte';
import { recordsEqual } from '../entity-store.svelte';
import { fieldsOf, type FieldConfig, type Identified } from '../entities/types';
import { fieldWarn } from '../validation';
import SelectCaret from './SelectCaret.svelte';
import NumInput from './NumInput.svelte';
import DirtyDot from './DirtyDot.svelte';
import AttributePicker from './AttributePicker.svelte';

interface Props {
	field: FieldConfig<Identified>;
	record: Identified;
	baseline: Identified | undefined;
	store: EntityStore<Identified>;
}

const { field, record, baseline, store }: Props = $props();

const key = $derived(field.key as string);
const value = $derived(fieldsOf(record)[key]);
const dirty = $derived(baseline ? !recordsEqual(value, fieldsOf(baseline)[key]) : false);
const warn = $derived(fieldWarn(field, record));

const set = (next: unknown) => {
	store.patch(record.id, (draft) => {
		fieldsOf(draft)[key] = next;
	});
};
</script>
