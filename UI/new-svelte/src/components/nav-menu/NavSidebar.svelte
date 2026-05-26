<div
	class="sidebar"
	class:expanded
	data-testid="sidebar"
	role="complementary"
	onmouseenter={() => hovering = true}
	onmouseleave={() => hovering = false}
>
	<!-- Wordmark / pin -->
	<div class="sidebar-header">
		<div class="glyph-slot">
			<div class="game-diamond pulse">
				<div class="game-diamond-inner"></div>
			</div>
		</div>
		<div class="wordmark" class:show={expanded}>Tactic Foundry</div>
		{#if expanded}
			<button
				class="pin-button"
				class:pinned
				data-testid="pin-button"
				title={pinned ? 'Unpin' : 'Keep open'}
				onclick={() => pinned = !pinned}
			>
				<svg width="13" height="13" viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round">
					{#if pinned}
						<path d="M9 1.5l3.5 3.5-2 2L7 3.5z" />
						<path d="M7 3.5l-3 3 3.5 3.5 3-3" />
						<path d="M4 6.5L1.5 12.5" />
					{:else}
						<path d="M10.5 1.5L12.5 3.5l-1.5 1.5L9 3z" />
						<path d="M9 3l-2.5 2.5 3 3 2.5-2.5" />
						<path d="M6.5 5.5L2 10" />
					{/if}
				</svg>
			</button>
		{/if}
	</div>

	<!-- Nav body -->
	<div class="sidebar-body">
		{#each groups as group, gi}
			{@const groupItems = screens.filter(s => s.group === group.key)}
			{#if groupItems.length}
				<div class="nav-group">
					<!-- Group header -->
					<div class="group-header" class:first={gi === 0}>
						{#if expanded}
							<span class="group-label" class:show={expanded}>{group.label}</span>
						{:else if gi > 0}
							<div class="glyph-slot">
								<div class="group-divider"></div>
							</div>
						{/if}
					</div>

					{#each groupItems as screen}
						<button
							class="side-item"
							class:active={active === screen.key}
							data-testid="sidebar-item-{screen.key}"
							title={!expanded ? screen.label : undefined}
							onclick={() => handleClick(screen)}
						>
							<div class="glyph-slot">
								<SideGlyph kind={screen.key} active={active === screen.key} />
							</div>
							<span class="item-label" class:show={expanded}>{screen.label}</span>
							{#if !screen.built}
								<span class="wip-badge" class:show={expanded}>wip</span>
							{/if}
							{#if active === screen.key}
								<span class="active-bar"></span>
							{/if}
						</button>
					{/each}
				</div>
			{/if}
		{/each}
	</div>

	<!-- Footer: tick counter -->
	<div class="sidebar-footer">
		<div class="glyph-slot">
			<div class="pulse-dot"></div>
		</div>
		<div class="tick-display" class:show={expanded}>
			<span title="Logic tick rate">L · {logicRate}</span>
			<span class="tick-sep">·</span>
			<span title="Render tick rate">R · {renderRate}</span>
		</div>
	</div>
</div>

<script lang="ts">
import { logicEngine, renderEngine } from '$lib/engine';
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
}

const { screens, active, onNavigate }: Props = $props();

let hovering = $state(false);
let pinned = $state(false);

const expanded = $derived(pinned || hovering);

const groups = [
	{ key: 'combat', label: 'Combat' },
	{ key: 'character', label: 'Character' },
	{ key: 'settings', label: 'Settings' },
	{ key: 'admin', label: 'Admin' },
];

const logicRate = $derived(logicEngine.tickRate);
const renderRate = $derived(renderEngine.tickRate);

const handleClick = (screen: ScreenDef) => {
	onNavigate(screen.key);
};
</script>

<style lang="scss">
$collapsed: 60px;
$expanded-width: 218px;

.sidebar {
	position: absolute;
	top: 0;
	bottom: 0;
	left: 0;
	width: $collapsed;
	background: #14151b;
	border-right: 1px solid var(--border-subtle);
	transition: width 220ms cubic-bezier(.4, 0, .2, 1), box-shadow 220ms ease;
	display: flex;
	flex-direction: column;
	z-index: 10;
	overflow: hidden;

	&.expanded {
		width: $expanded-width;
		box-shadow: 6px 0 28px rgba(0, 0, 0, 0.55);
	}
}

.sidebar-header {
	padding: 18px 0;
	border-bottom: 1px solid rgba(255, 255, 255, 0.06);
	position: relative;
	display: flex;
	align-items: center;
	height: 58px;
}

.glyph-slot {
	width: $collapsed;
	flex-shrink: 0;
	display: flex;
	justify-content: center;
	align-items: center;
}

.game-diamond {
	width: 13px;
	height: 13px;
	transform: rotate(45deg);
	border: 1px solid var(--accent);
	box-shadow: 0 0 8px rgba(161, 194, 247, 0.35);
	position: relative;

	&.pulse {
		animation: pulse-glow 1.8s ease-in-out infinite;
	}
}

.game-diamond-inner {
	position: absolute;
	inset: 3px;
	background: var(--accent);
}

.wordmark {
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.92);
	white-space: nowrap;
	opacity: 0;
	transition: opacity 180ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 90ms;
	}
}

.pin-button {
	position: absolute;
	right: 12px;
	top: 50%;
	transform: translateY(-50%);
	background: transparent;
	border: none;
	padding: 6px;
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	color: rgba(240, 240, 240, 0.5);
	transition: color 140ms;

	&:hover {
		color: var(--text-primary);
	}

	&.pinned {
		color: var(--accent);
	}
}

.sidebar-body {
	flex: 1;
	overflow-y: auto;
	overflow-x: hidden;
	padding: 12px 0;
}

.nav-group {
	margin-bottom: 8px;
}

.group-header {
	display: flex;
	align-items: center;

	&:not(.first) {
		margin-top: 6px;
	}
}

.group-label {
	padding: 8px 22px 4px;
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
	color: var(--text-muted);
	white-space: nowrap;
	opacity: 0;
	transition: opacity 160ms ease;
	transition-delay: 0ms;

	&.show {
		opacity: 1;
		transition-delay: 80ms;
	}
}

.group-divider {
	width: 18px;
	height: 1px;
	background: rgba(240, 240, 240, 0.12);
}

.side-item {
	position: relative;
	width: 100%;
	background: transparent;
	border: none;
	color: rgba(240, 240, 240, 0.65);
	font-family: inherit;
	font-size: 13px;
	padding: 0;
	cursor: pointer;
	text-align: left;
	display: flex;
	align-items: center;
	height: 38px;
	transition: color 140ms, background 140ms;
	white-space: nowrap;
	overflow: hidden;

	&:hover {
		background: rgba(255, 255, 255, 0.03);
		color: var(--text-primary);
	}

	&.active {
		background: rgba(161, 194, 247, 0.08);
		color: var(--text-primary);
	}
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

.wip-badge {
	font-family: 'Geist Mono', monospace;
	font-size: 8.5px;
	color: rgba(240, 240, 240, 0.42);
	letter-spacing: 0.5px;
	padding: 1px 4px;
	border: 1px solid rgba(240, 240, 240, 0.15);
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

.active-bar {
	position: absolute;
	left: 0;
	top: 5px;
	bottom: 5px;
	width: 2px;
	background: var(--accent);
	box-shadow: 0 0 10px rgba(161, 194, 247, 0.75);
}

.sidebar-footer {
	border-top: 1px solid rgba(255, 255, 255, 0.06);
	display: flex;
	align-items: center;
	height: 44px;
	flex-shrink: 0;
}

.pulse-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	background: var(--accent);
	box-shadow: 0 0 8px rgba(161, 194, 247, 0.8);
	animation: pulse-dot 1.6s ease-in-out infinite;
}

.tick-display {
	display: flex;
	align-items: center;
	gap: 10px;
	font-family: 'Geist Mono', monospace;
	font-size: 10.5px;
	color: rgba(240, 240, 240, 0.55);
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
