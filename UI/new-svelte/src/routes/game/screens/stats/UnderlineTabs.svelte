<!-- The shared underline tab bar used by both the category tabs ("by statistic")
     and the entity-kind tabs ("by entity"), so the two views read as one system.
     Each tab carries its own accent colour and an optional count pill / glyph. -->
<div class="tabs" role="tablist">
	{#each tabs as tab (tab.key)}
		{@const on = tab.key === active}
		<button
			type="button"
			class="tab"
			class:on
			role="tab"
			aria-selected={on}
			data-testid="tab-{tab.key}"
			style:--tab-color={tab.color}
			onclick={() => onChange(tab.key)}
		>
			{#if tab.glyphKind}
				<StatGlyph kind={tab.glyphKind} size={13} color={on ? tab.color : 'var(--text-tertiary)'} />
			{/if}
			<span class="label">{tab.label}</span>
			{#if tab.count != null}
				<span class="count">{tab.count}</span>
			{/if}
			{#if on}
				<span class="underline"></span>
			{/if}
		</button>
	{/each}
</div>

<script lang="ts">
import StatGlyph from './StatGlyph.svelte';
import type { StatEntityKind } from './statistics-view.svelte';

export interface Tab {
	key: string;
	label: string;
	count?: number;
	/** Accent colour (a `var(--…)` token). */
	color: string;
	/** Optional entity glyph rendered before the label. */
	glyphKind?: StatEntityKind;
}

interface Props {
	tabs: Tab[];
	active: string;
	onChange: (key: string) => void;
}

let { tabs, active, onChange }: Props = $props();
</script>

<style lang="scss">
.tabs {
	display: flex;
	gap: 6px;
	border-bottom: 1px solid var(--border-subtle);
}

.tab {
	position: relative;
	background: transparent;
	border: none;
	cursor: pointer;
	padding: 4px 14px 12px;
	display: flex;
	align-items: center;
	gap: 8px;

	.label {
		font-family: var(--sans);
		font-size: 13.5px;
		font-weight: 400;
		color: var(--text-tertiary);
		transition: color 120ms;
	}

	&.on .label {
		font-weight: 600;
		color: var(--text-primary);
	}
}

.count {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
	border: 1px solid var(--border-light);
	border-radius: 8px;
	padding: 0 6px;
	line-height: 15px;

	.on & {
		color: var(--tab-color);
		border-color: color-mix(in srgb, var(--tab-color) 45%, transparent);
	}
}

.underline {
	position: absolute;
	left: 0;
	right: 0;
	bottom: -1px;
	height: 2px;
	background: var(--tab-color);
	border-radius: 2px;
}
</style>
