<div class="detail-head">
	{#if crumb}{@render crumb()}{/if}
	<div class="head-row">
		<span class="rec-id" class:is-new={isNew}>{idLabel}</span>
		<h2 class="rec-name" class:blank={!name}>{name || blank}</h2>
		{#if badge}
			<span class="rare-tag" style:color={badgeColor ?? 'var(--text-secondary)'}>{badge}</span>
		{/if}
		{#if status === 'added'}<span class="spill added">new</span>{/if}
		{#if status === 'modified'}<span class="spill modified">edited</span>{/if}
		{#if status === 'deleted'}<span class="spill deleted">removed</span>{/if}
		{#if retired}<span class="spill retired">retired</span>{/if}
		<div class="spacer"></div>
		{#if status === 'deleted' && onRestore}
			<button type="button" class="hdr-action accent" onclick={() => onRestore?.()}>
				<WorkbenchIcon kind="check" size={11} sw={1.7} />Restore
			</button>
		{:else}
			{#if status === 'modified' && onReset}
				<button type="button" class="hdr-action reset" onclick={() => onReset?.()}>Reset</button>
			{/if}
			{#if retireable}
				{#if isNew}
					<button type="button" class="hdr-action danger" onclick={() => onRemove?.()}>
						<WorkbenchIcon kind="x" size={11} />Remove
					</button>
				{:else if retired}
					<button type="button" class="hdr-action accent" onclick={() => onReinstate?.()}>
						<WorkbenchIcon kind="check" size={11} sw={1.7} />Reinstate
					</button>
				{:else}
					<button type="button" class="hdr-action danger" onclick={() => onRetire?.()}>
						<WorkbenchIcon kind="archive" size={11} />Retire
					</button>
				{/if}
			{:else if onDelete}
				<button type="button" class="hdr-action danger" onclick={() => onDelete?.()}>
					<WorkbenchIcon kind="x" size={11} />Delete
				</button>
			{/if}
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

/** A detail-pane tab descriptor (shared header chrome). */
export interface DetailHeaderTab {
	key: string;
	label: string;
	count?: number | null;
	dirty?: boolean;
	warn?: boolean;
	disabled?: boolean;
}

interface Props {
	idLabel: string;
	isNew: boolean;
	name: string;
	blank: string;
	badge?: string | null;
	badgeColor?: string;
	status: 'added' | 'modified' | 'deleted' | 'clean';
	/** Whether this record uses retire/reinstate (the default) rather than a hard delete. */
	retireable?: boolean;
	retired: boolean;
	headline?: string;
	tabs: DetailHeaderTab[];
	activeTab: string;
	onTab: (key: string) => void;
	onReset?: () => void;
	onRetire?: () => void;
	onReinstate?: () => void;
	onRemove?: () => void;
	/** Hard-delete action for a non-retireable entity; ignored when {@link retireable} is true. */
	onDelete?: () => void;
	/** Restores a soft-deleted (not-yet-saved-delete) record; only relevant for `status: 'deleted'`. */
	onRestore?: () => void;
	crumb?: Snippet;
}

const {
	idLabel,
	isNew,
	name,
	blank,
	badge,
	badgeColor,
	status,
	retireable = true,
	retired,
	headline,
	tabs,
	activeTab,
	onTab,
	onReset,
	onRetire,
	onReinstate,
	onRemove,
	onDelete,
	onRestore,
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
.tabs {
	display: flex;
	gap: 4px;
	margin-top: 16px;
	border-bottom: 1px solid var(--border-subtle);
	flex-wrap: wrap;
}
</style>
