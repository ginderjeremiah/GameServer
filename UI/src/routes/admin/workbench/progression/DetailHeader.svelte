<div class="detail-head">
	{#if crumb}{@render crumb()}{/if}
	<div class="head-row">
		<span class="rec-id" class:is-new={isNew}>{idLabel}</span>
		<h2 class="rec-name" class:blank={!name}>{name || blank}</h2>
		{#if status === 'added'}<span class="spill added">new</span>{/if}
		{#if status === 'modified'}<span class="spill modified">edited</span>{/if}
		{#if retired}<span class="spill retired">retired</span>{/if}
		<div class="spacer"></div>
		{#if status === 'modified' && onReset}
			<button type="button" class="hdr-action" onclick={onReset}>Reset</button>
		{/if}
		{#if isNew}
			<button type="button" class="hdr-action danger" onclick={onRemove}>
				<WorkbenchIcon kind="x" size={11} />Remove
			</button>
		{:else if retired}
			<button type="button" class="hdr-action accent" onclick={onReinstate}>
				<WorkbenchIcon kind="check" size={11} sw={1.7} />Reinstate
			</button>
		{:else}
			<button type="button" class="hdr-action danger" onclick={onRetire}>
				<WorkbenchIcon kind="archive" size={11} />Retire
			</button>
		{/if}
	</div>

	{#if headline}<div class="headline">{headline}</div>{/if}

	<div class="tabs">
		{#each tabs as t (t.key)}
			<button
				type="button"
				class="tab"
				class:active={t.key === activeTab}
				class:disabled={t.disabled}
				disabled={t.disabled}
				onclick={() => onTab(t.key)}
			>
				{t.label}
				{#if t.count != null}<span class="count-badge">{t.count}</span>{/if}
				{#if t.warn}<WorkbenchIcon kind="warn" size={11} stroke="var(--warning)" />{/if}
				{#if t.dirty}<span class="tab-dot" aria-hidden="true"></span><span class="sr-only">Unsaved changes</span>{/if}
			</button>
		{/each}
	</div>
</div>

<script lang="ts">
import type { Snippet } from 'svelte';
import WorkbenchIcon from '../WorkbenchIcon.svelte';
import type { ProgressionTab } from './types';

interface Props {
	idLabel: string;
	isNew: boolean;
	name: string;
	blank: string;
	status: 'added' | 'modified' | 'clean';
	retired: boolean;
	headline?: string;
	tabs: ProgressionTab[];
	activeTab: string;
	onTab: (key: string) => void;
	onReset?: () => void;
	onRetire: () => void;
	onReinstate: () => void;
	onRemove: () => void;
	crumb?: Snippet;
}

const {
	idLabel,
	isNew,
	name,
	blank,
	status,
	retired,
	headline,
	tabs,
	activeTab,
	onTab,
	onReset,
	onRetire,
	onReinstate,
	onRemove,
	crumb
}: Props = $props();
</script>

<style lang="scss">
.detail-head {
	padding: 18px 32px 0;
	flex-shrink: 0;
}
.head-row {
	display: flex;
	align-items: center;
	gap: 12px;
}
.spacer {
	flex: 1;
}
.rec-id {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);

	&.is-new {
		color: var(--accent);
	}
}
.rec-name {
	margin: 0;
	font-size: 22px;
	font-weight: 500;
	color: var(--text-primary);

	&.blank {
		color: var(--text-muted);
	}
}
.headline {
	margin-top: 8px;
	font-size: 14px;
	color: var(--text-secondary);
	letter-spacing: -0.1px;
}
.spill {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1px;
	text-transform: uppercase;
	padding: 2px 6px;
	border-radius: 3px;

	&.added {
		color: var(--change-added);
		border: 1px solid color-mix(in srgb, var(--change-added) 40%, transparent);
		background: color-mix(in srgb, var(--change-added) 10%, transparent);
	}
	&.modified {
		color: var(--change-modified);
		border: 1px solid color-mix(in srgb, var(--change-modified) 40%, transparent);
		background: color-mix(in srgb, var(--change-modified) 10%, transparent);
	}
	&.retired {
		color: var(--text-muted);
		border: 1px solid var(--border-light);
	}
}
.hdr-action {
	display: inline-flex;
	align-items: center;
	gap: 6px;
	background: transparent;
	border: 1px solid var(--border-light);
	color: var(--text-tertiary);
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.6px;
	text-transform: uppercase;
	padding: 6px 11px;
	border-radius: 3px;
	cursor: pointer;

	&:hover {
		border-color: color-mix(in srgb, var(--white) 28%, transparent);
		color: var(--text-secondary);
	}
	&.danger:hover {
		border-color: var(--change-removed);
		color: var(--change-removed);
	}
	&.accent {
		border-color: var(--accent);
		color: var(--accent-light);
	}
}
.tabs {
	display: flex;
	gap: 4px;
	margin-top: 16px;
	border-bottom: 1px solid var(--border-subtle);
	flex-wrap: wrap;
}
.tab {
	display: flex;
	align-items: center;
	gap: 8px;
	background: transparent;
	border: none;
	border-bottom: 2px solid transparent;
	color: var(--text-tertiary);
	font-family: var(--sans);
	font-size: 13px;
	padding: 8px 14px 12px;
	cursor: pointer;

	&.active {
		color: var(--text-primary);
		border-bottom-color: var(--accent);
	}
	&.disabled {
		color: var(--text-muted);
		cursor: not-allowed;
	}
}
.count-badge {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-tertiary);
	background: color-mix(in srgb, var(--white) 5%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 10px;
	padding: 1px 8px;
	min-width: 22px;
	text-align: center;
}
.tab-dot {
	width: 5px;
	height: 5px;
	border-radius: 50%;
	background: var(--change-modified);
	box-shadow: 0 0 5px var(--change-modified);
}
</style>
