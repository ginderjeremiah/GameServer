<CollapsibleRail testid="sidebar" pinTestid="pin-button" bind:pinned>
	{#snippet brand(expanded)}
		<div class="wordmark" class:show={expanded}>Name TBD</div>
	{/snippet}

	{#snippet body(expanded)}
		{#each groups as group, gi (group.key)}
			{@const groupItems = screens.filter((s) => s.group === group.key)}
			{#if groupItems.length}
				<RailNavGroup label={group.label} first={gi === 0} {expanded}>
					{#each groupItems as screen (screen.key)}
						<RailNavItem
							active={active === screen.key}
							label={screen.label}
							title={screen.label}
							testid="sidebar-item-{screen.key}"
							{expanded}
							onclick={() => onNavigate(screen.key)}
						>
							{#snippet glyph(isActive)}
								<SideGlyph kind={screen.key} active={isActive} />
							{/snippet}
							{#snippet trailing()}
								{#if screen.key === 'help' && unreadLessonCount > 0}
									<span class="unread-badge" class:show={expanded} title="{unreadLessonCount} unread lesson(s)">
										{unreadLessonCount}
									</span>
								{:else if !screen.built}
									<span class="wip-badge" class:show={expanded}>wip</span>
								{/if}
							{/snippet}
						</RailNavItem>
					{/each}
				</RailNavGroup>
			{/if}
		{/each}
	{/snippet}

	{#snippet footer(expanded)}
		<div class="tick-display" class:show={expanded}>
			<span title="Logic tick rate">L {logicRate}</span>
			<span class="tick-sep">·</span>
			<span title="Render tick rate">R {renderRate}</span>
			<span class="tick-sep">·</span>
			<span title="Server Ping">{parseFloat(ping.toFixed(3)).toString()} ms</span>
		</div>
	{/snippet}
</CollapsibleRail>

<script lang="ts">
import { onPingMeasured } from '$lib/api';
import { logicEngine, playerManager, renderEngine } from '$lib/engine';
import CollapsibleRail from '../sidebar/CollapsibleRail.svelte';
import RailNavGroup from '../sidebar/RailNavGroup.svelte';
import RailNavItem from '../sidebar/RailNavItem.svelte';
import SideGlyph from './SideGlyph.svelte';

interface ScreenDef {
	key: string;
	label: string;
	group: string;
	built: boolean;
}

interface Props {
	screens: ScreenDef[];
	active: string;
	onNavigate: (key: string) => void;
	/** Two-way bound so the parent layout can reserve space for a pinned rail. */
	pinned?: boolean;
}

let { screens, active, onNavigate, pinned = $bindable(false) }: Props = $props();

let ping = $state(0);

// Subscribed during component init, so opt into the hook's onDestroy cleanup.
onPingMeasured((p) => (ping = p), true);

const groups = [
	{ key: 'combat', label: 'Combat' },
	{ key: 'character', label: 'Character' },
	{ key: 'settings', label: 'Settings' },
	{ key: 'admin', label: 'Admin' }
];

const logicRate = $derived(logicEngine.tickRate);

// Persistent unread-lesson indicator (spike #1392, #1587) — deliberately distinct from the wip badge
// and the toast channel, since a queued mechanic lesson isn't a "not built yet" marker or a
// transient notification.
const unreadLessonCount = $derived(playerManager.lessons.filter((lesson) => !lesson.readAt).length);
const renderRate = $derived(renderEngine.tickRate);
</script>

<style lang="scss">
.wordmark {
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 92%, transparent);
	white-space: nowrap;
	opacity: 0;
	transition: opacity 180ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 90ms;
	}
}

.wip-badge {
	font-family: var(--mono);
	font-size: 8.5px;
	color: color-mix(in srgb, var(--text-primary) 42%, transparent);
	letter-spacing: 0.5px;
	padding: 1px 4px;
	border: 1px solid color-mix(in srgb, var(--text-primary) 15%, transparent);
	border-radius: 2px;
	text-transform: uppercase;
	margin-right: 14px;
	opacity: 0;
	transition: opacity 160ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 110ms;
	}
}

.unread-badge {
	display: inline-flex;
	align-items: center;
	justify-content: center;
	min-width: 16px;
	height: 16px;
	padding: 0 4px;
	font-family: var(--mono);
	font-size: 9.5px;
	font-weight: 600;
	color: var(--text-on-accent);
	background: var(--accent);
	border-radius: 8px;
	margin-right: 14px;
	opacity: 0;
	transition: opacity 160ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 110ms;
	}
}

.tick-display {
	display: flex;
	align-items: center;
	gap: 10px;
	font-family: var(--mono);
	font-size: 10.5px;
	color: var(--text-tertiary);
	letter-spacing: 0.5px;
	white-space: nowrap;
	opacity: 0;
	transition: opacity 160ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 90ms;
	}
}

.tick-sep {
	opacity: 0.4;
}
</style>
