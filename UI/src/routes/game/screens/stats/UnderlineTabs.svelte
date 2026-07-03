<!-- The Statistics screen's underline tab bar (the category tabs). Each tab
     carries its own accent colour and an optional count pill. -->
<div class="tabs" role="tablist">
	{#each tabs as tab (tab.key)}
		{@const on = tab.key === active}
		<button
			type="button"
			class="tab"
			class:on
			role="tab"
			aria-selected={on}
			id={panelId ? `${panelId}-tab-${tab.key}` : undefined}
			aria-controls={panelId}
			data-testid="tab-{tab.key}"
			style:--tab-color={tab.color}
			onclick={() => onChange(tab.key)}
		>
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
export interface Tab {
	key: string;
	label: string;
	count?: number;
	/** Accent colour (a `var(--…)` token). */
	color: string;
}

interface Props {
	tabs: Tab[];
	active: string;
	onChange: (key: string) => void;
	/** Id of the `role="tabpanel"` these tabs control; when set, each tab gets a stable id +
	 *  `aria-controls` so the panel can point its `aria-labelledby` back at the active tab. */
	panelId?: string;
}

let { tabs, active, onChange, panelId }: Props = $props();
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
