<div
	class="sidebar"
	class:expanded
	data-testid="admin-sidebar"
	role="complementary"
	onmouseenter={() => (hovering = true)}
	onmouseleave={() => (hovering = false)}
>
	<!-- Wordmark + admin tag + pin -->
	<div class="sidebar-header">
		<div class="glyph-slot">
			<div class="game-diamond pulse">
				<div class="game-diamond-inner"></div>
			</div>
		</div>
		<div class="wordmark-block" class:show={expanded}>
			<div class="wordmark">Tactic Foundry</div>
			<div class="wordmark-tag">Admin Console</div>
		</div>
		{#if expanded}
			<button
				class="pin-button"
				class:pinned
				data-testid="admin-pin-button"
				title={pinned ? 'Unpin' : 'Keep open'}
				onclick={() => (pinned = !pinned)}
			>
				<svg
					width="13"
					height="13"
					viewBox="0 0 14 14"
					fill="none"
					stroke="currentColor"
					stroke-width="1.4"
					stroke-linecap="round"
					stroke-linejoin="round"
				>
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
		{#each groups as group, gi (group.key)}
			{@const groupTools = tools.filter((t) => t.group === group.key)}
			{#if groupTools.length}
				<div class="nav-group">
					<div class="group-header" class:first={gi === 0}>
						{#if expanded}
							<span class="group-label">{group.label}</span>
						{:else if gi > 0}
							<div class="glyph-slot">
								<div class="group-divider"></div>
							</div>
						{/if}
					</div>

					{#each groupTools as tool (tool.key)}
						<button
							class="side-item"
							class:active={active === tool.key}
							data-testid="admin-tool-{tool.key}"
							title={!expanded ? tool.label : undefined}
							onclick={() => onNavigate(tool.key)}
						>
							<div class="glyph-slot">
								<AdminGlyph kind={tool.glyph} active={active === tool.key} />
							</div>
							<span class="item-label" class:show={expanded}>{tool.label}</span>
							{#if active === tool.key}
								<span class="active-bar"></span>
							{/if}
						</button>
					{/each}
				</div>
			{/if}
		{/each}
	</div>

	<!-- Return to game -->
	<div class="sidebar-return">
		<button
			class="side-item"
			data-testid="admin-return-to-game"
			title={!expanded ? 'Return to Game' : undefined}
			onclick={onBackToGame}
		>
			<div class="glyph-slot">
				<AdminGlyph kind="back" />
			</div>
			<span class="item-label" class:show={expanded}>Return to Game</span>
		</button>
	</div>

	<!-- Footer: status -->
	<div class="sidebar-footer">
		<div class="glyph-slot">
			<div class="pulse-dot"></div>
		</div>
		<div class="footer-status" class:show={expanded}>{tools.length} tools</div>
	</div>
</div>

<script lang="ts">
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

let hovering = $state(false);
const expanded = $derived(pinned || hovering);
</script>

<style lang="scss">
$collapsed: 60px;
$expanded-width: 240px;

.sidebar {
	position: absolute;
	top: 0;
	bottom: 0;
	left: 0;
	width: $collapsed;
	background: #14151b;
	border-right: 1px solid var(--border-subtle);
	transition:
		width 220ms cubic-bezier(0.4, 0, 0.2, 1),
		box-shadow 220ms ease;
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
	padding: 14px 0;
	border-bottom: 1px solid rgba(255, 255, 255, 0.06);
	position: relative;
	display: flex;
	align-items: center;
	height: 64px;
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
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.92);
}

.wordmark-tag {
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 2.4px;
	text-transform: uppercase;
	color: var(--accent);
	margin-top: 2px;
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
	transition:
		color 140ms,
		background 140ms;
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
	box-shadow: 0 0 10px rgba(161, 194, 247, 0.75);
}

.sidebar-return {
	border-top: 1px solid rgba(255, 255, 255, 0.06);
	padding: 6px 0;
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

.footer-status {
	font-family: 'Geist Mono', monospace;
	font-size: 10.5px;
	color: rgba(240, 240, 240, 0.55);
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
