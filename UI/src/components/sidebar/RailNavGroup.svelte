<div class="nav-group">
	<div class="group-header" class:first>
		{#if expanded}
			<span class="group-label">{label}</span>
		{:else if !first}
			<div class="glyph-slot">
				<div class="group-divider"></div>
			</div>
		{/if}
	</div>

	{@render children()}
</div>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** Section heading shown when the rail is expanded. */
	label: string;
	/** Whether this is the first rendered group (suppresses the top margin / collapsed divider). */
	first?: boolean;
	/** The rail's expanded state. */
	expanded: boolean;
	/** The group's nav items. */
	children: Snippet;
}

const { label, first = false, expanded, children }: Props = $props();
</script>

<style lang="scss">
$collapsed: 60px;

.nav-group {
	margin-bottom: 8px;
}

.group-header {
	display: flex;
	align-items: center;
	// Reserve the expanded label's height in the collapsed state too, so the nav buttons keep the
	// same vertical position whether the rail is collapsed or expanded — only the label (expanded)
	// or divider (collapsed) swaps within this fixed slot. Previously the header was ~0px collapsed
	// and ~23px expanded, sliding every button below it down when the rail opened.
	height: 23px;

	&:not(.first) {
		margin-top: 6px;
	}
}

.group-label {
	padding: 0 22px;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	white-space: nowrap;
}

.glyph-slot {
	width: $collapsed;
	flex-shrink: 0;
	display: flex;
	justify-content: center;
	align-items: center;
}

.group-divider {
	width: 18px;
	height: 1px;
	background: color-mix(in srgb, var(--text-primary) 12%, transparent);
}
</style>
