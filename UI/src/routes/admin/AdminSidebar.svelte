<CollapsibleRail testid="admin-sidebar" pinTestid="admin-pin-button" headerHeight={64} headerPadding={14} bind:pinned>
	{#snippet brand(expanded)}
		<div class="wordmark-block" class:show={expanded}>
			<div class="wordmark">Name TBD</div>
			<div class="wordmark-tag">Admin Console</div>
		</div>
	{/snippet}

	{#snippet body(expanded)}
		{#each groups as group, gi (group.key)}
			{@const groupTools = tools.filter((t) => t.group === group.key)}
			{#if groupTools.length}
				<RailNavGroup label={group.label} first={gi === 0} {expanded}>
					{#each groupTools as tool (tool.key)}
						<RailNavItem
							active={active === tool.key}
							label={tool.label}
							title={tool.label}
							testid="admin-tool-{tool.key}"
							{expanded}
							onclick={() => onNavigate(tool.key)}
						>
							{#snippet glyph(isActive)}
								<AdminGlyph kind={tool.glyph} active={isActive} />
							{/snippet}
						</RailNavItem>
					{/each}
				</RailNavGroup>
			{/if}
		{/each}
	{/snippet}

	{#snippet aside(expanded)}
		<RailNavItem
			label="Return to Game"
			title="Return to Game"
			testid="admin-return-to-game"
			{expanded}
			onclick={onBackToGame}
		>
			{#snippet glyph()}
				<AdminGlyph kind="back" />
			{/snippet}
		</RailNavItem>
	{/snippet}

	{#snippet footer(expanded)}
		<div class="footer-status" class:show={expanded}>{tools.length} tools</div>
	{/snippet}
</CollapsibleRail>

<script lang="ts">
import CollapsibleRail from '$components/sidebar/CollapsibleRail.svelte';
import RailNavGroup from '$components/sidebar/RailNavGroup.svelte';
import RailNavItem from '$components/sidebar/RailNavItem.svelte';
import AdminGlyph from './AdminGlyph.svelte';
import type { AdminGroupDef, AdminToolDef } from './workbench/nav';

interface Props {
	tools: AdminToolDef[];
	groups: AdminGroupDef[];
	active: string;
	onNavigate: (key: string) => void;
	onBackToGame: () => void;
	/** Two-way bound so the parent layout can reserve space for a pinned rail. */
	pinned?: boolean;
}

let { tools, groups, active, onNavigate, onBackToGame, pinned = $bindable(false) }: Props = $props();
</script>

<style lang="scss">
.wordmark-block {
	line-height: 1.35;
	white-space: nowrap;
	opacity: 0;
	transition: opacity 180ms ease;

	&.show {
		opacity: 1;
		transition-delay: 90ms;
	}
}

.wordmark {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 92%, transparent);
}

.wordmark-tag {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 2.4px;
	text-transform: uppercase;
	color: var(--accent);
	margin-top: 2px;
}

.footer-status {
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);
	letter-spacing: 0.5px;
	white-space: nowrap;
	opacity: 0;
	transition: opacity 160ms ease;

	&.show {
		opacity: 1;
		transition-delay: 90ms;
	}
}
</style>
