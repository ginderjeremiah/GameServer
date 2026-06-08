<button
	class="side-item"
	class:active
	data-testid={testid}
	title={!expanded ? title : undefined}
	{onclick}
>
	<div class="glyph-slot">
		{@render glyph(active)}
	</div>
	<span class="item-label" class:show={expanded}>{label}</span>
	{@render trailing?.(expanded)}
	{#if active}
		<span class="active-bar"></span>
	{/if}
</button>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Whether this item is the active screen/tool. */
	active?: boolean;
	/** Visible label, shown when expanded. */
	label: string;
	/** Tooltip title shown only when the rail is collapsed. */
	title: string;
	/** data-testid for the button. */
	testid: string;
	/** The rail's expanded state. */
	expanded: boolean;
	onclick: () => void;
	/** Renders the item's icon. Receives the active state so the glyph can highlight. */
	glyph: Snippet<[boolean]>;
	/** Optional trailing content (e.g. a "wip" badge). Receives the expanded state. */
	trailing?: Snippet<[boolean]>;
}

const { active = false, label, title, testid, expanded, onclick, glyph, trailing }: Props = $props();
</script>

<style lang="scss">
$collapsed: 60px;

.side-item {
	position: relative;
	width: 100%;
	background: transparent;
	border: none;
	color: color-mix(in srgb, var(--text-primary) 65%, transparent);
	font-family: inherit;
	font-size: 13px;
	padding: 0;
	cursor: pointer;
	text-align: left;
	display: flex;
	align-items: center;
	height: 38px;
	transition:
		color 140ms,
		background 140ms;
	white-space: nowrap;
	overflow: hidden;

	&:hover {
		background: color-mix(in srgb, var(--white) 3%, transparent);
		color: var(--text-primary);
	}

	&.active {
		background: color-mix(in srgb, var(--accent) 8%, transparent);
		color: var(--text-primary);
	}
}

.glyph-slot {
	width: $collapsed;
	flex-shrink: 0;
	display: flex;
	justify-content: center;
	align-items: center;
}

.item-label {
	flex: 1;
	opacity: 0;
	transition: opacity 160ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 90ms;
	}
}

.active-bar {
	position: absolute;
	left: 0;
	top: 5px;
	bottom: 5px;
	width: 2px;
	background: var(--accent);
	box-shadow: 0 0 10px color-mix(in srgb, var(--accent) 75%, transparent);
}
</style>
