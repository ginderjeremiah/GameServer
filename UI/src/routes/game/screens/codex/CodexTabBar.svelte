<!-- The Codex's top-level tab bar: Enemies / Zones / Skills, each with a live count badge, section
     accent dot and an active underline. -->
<div class="tabs" role="tablist" aria-label="Codex sections">
	{#each view.tabs as tab (tab.key)}
		<button
			type="button"
			role="tab"
			aria-selected={tab.active}
			class="tab"
			class:active={tab.active}
			style:--tab-accent={tab.accent}
			data-testid="codex-tab-{tab.key}"
			onclick={() => view.selectTab(tab.key)}
		>
			<span class="dot"></span>
			<span class="label">{tab.label}</span>
			<span class="count">{tab.count}</span>
			<span class="underline"></span>
		</button>
	{/each}
</div>

<script lang="ts">
import type { CodexView } from './codex-view.svelte';

interface Props {
	view: CodexView;
}

let { view }: Props = $props();
</script>

<style lang="scss">
.tabs {
	display: flex;
	gap: 6px;
	border-bottom: 1px solid var(--border-subtle);
	padding: 0 30px;
	flex: none;
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
}

.dot {
	width: 8px;
	height: 8px;
	transform: rotate(45deg);
	flex: none;
	background: var(--text-muted);
}

.label {
	font-size: 13.5px;
	font-weight: 400;
	color: var(--text-tertiary);
}

.count {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
	border: 1px solid var(--border-light);
	border-radius: 8px;
	padding: 0 6px;
	line-height: 15px;
}

.underline {
	position: absolute;
	left: 0;
	right: 0;
	bottom: -1px;
	height: 2px;
	border-radius: 2px;
	background: transparent;
}

.tab.active {
	.dot {
		background: var(--tab-accent);
	}

	.label {
		color: var(--text-primary);
		font-weight: 600;
	}

	.count {
		color: var(--tab-accent);
		border-color: color-mix(in srgb, var(--tab-accent) 45%, transparent);
	}

	.underline {
		background: var(--tab-accent);
	}
}
</style>
