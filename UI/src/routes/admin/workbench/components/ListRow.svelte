<button
	type="button"
	class="list-row"
	data-testid={testId}
	class:selected
	class:deleted={status === 'deleted'}
	class:retired
	onclick={onSelect}
>
	<div
		class="row-edge"
		class:added={status === 'added'}
		class:modified={status === 'modified'}
		class:removed={status === 'deleted'}
	></div>
	{#if leading}{@render leading()}{/if}
	<div class="row-body">
		<div class="row-top">
			<span class="row-name" class:blank={!name}>{name || blank}</span>
			{#if badge}
				<span class="rare-tag" style:color={badgeColor ?? 'var(--text-secondary)'}>{badge}</span>
			{/if}
			{#if status === 'added'}<span class="spill added">new</span>{/if}
			{#if status === 'modified'}<span class="spill modified">edited</span>{/if}
			{#if retired}<span class="spill retired">retired</span>{/if}
			<div class="spacer"></div>
			{#if status !== 'deleted' && warnings.length > 0}
				<WarnTriangle title={warnings.join(' · ')} />
			{/if}
		</div>
		<span class="meta">{@render meta()}</span>
	</div>
</button>

<script lang="ts">
import type { Snippet } from 'svelte';
import WarnTriangle from './WarnTriangle.svelte';

/** A record's change status, driving the edge accent and the added/modified spill badge. */
export type ListRowStatus = 'clean' | 'added' | 'modified' | 'deleted';

interface Props {
	/** `data-testid` on the row button — each tool picks its own (e.g. `workbench-row`). */
	testId?: string;
	selected: boolean;
	status: ListRowStatus;
	retired?: boolean;
	name: string;
	blank: string;
	badge?: string | null;
	badgeColor?: string;
	warnings?: string[];
	onSelect: () => void;
	/** Leading content ahead of the row body — e.g. progression's tier ordinal circle. */
	leading?: Snippet;
	meta: Snippet;
}

const {
	testId,
	selected,
	status,
	retired = false,
	name,
	blank,
	badge,
	badgeColor,
	warnings = [],
	onSelect,
	leading,
	meta
}: Props = $props();
</script>

<style lang="scss">
.list-row {
	width: 100%;
	text-align: left;
	background: transparent;
	border: none;
	border-bottom: 1px solid var(--border-subtle);
	padding: 10px 14px 10px 0;
	cursor: pointer;
	display: flex;
	align-items: center;
	gap: 10px;

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
	}
	&.selected {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
	}
	&.deleted {
		opacity: 0.45;
	}
	// Retired records stay in the catalogue (still resolvable by id), just dimmed — distinct
	// from the stronger fade + strikethrough of a pending hard-delete.
	&.retired:not(.deleted) {
		opacity: 0.6;
	}
	// Cascade the row's own change-state onto the name text rather than threading a second
	// selected/struck prop down to every caller.
	&.selected .row-name {
		color: var(--text-primary);
		font-weight: 500;
	}
	&.deleted .row-name {
		text-decoration: line-through;
	}
}
.row-edge {
	width: 3px;
	height: 34px;
	flex-shrink: 0;
	border-radius: 2px;
	background: transparent;

	&.added {
		background: var(--change-added);
		box-shadow: 0 0 7px var(--change-added);
	}
	&.modified {
		background: var(--change-modified);
		box-shadow: 0 0 7px var(--change-modified);
	}
	&.removed {
		background: var(--change-removed);
		box-shadow: 0 0 7px var(--change-removed);
	}
}
.row-body {
	flex: 1;
	min-width: 0;
}
.row-top {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 4px;
}
.row-name {
	font-size: 13.5px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	&.blank {
		color: var(--text-muted);
		font-style: italic;
	}
}
</style>
