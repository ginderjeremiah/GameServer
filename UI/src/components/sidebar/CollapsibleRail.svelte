<div
	class="sidebar"
	class:expanded
	data-testid={testid}
	role="complementary"
	style:--rail-header-height="{headerHeight}px"
	style:--rail-header-padding="{headerPadding}px"
	onmouseenter={() => (hovering = true)}
	onmouseleave={() => (hovering = false)}
	onfocusin={() => (focused = true)}
	onfocusout={() => (focused = false)}
>
	<!-- Header: brand wordmark + pin toggle -->
	<div class="sidebar-header">
		<div class="glyph-slot">
			<div class="game-diamond pulse">
				<div class="game-diamond-inner"></div>
			</div>
		</div>
		{@render brand(expanded)}
		{#if expanded}
			<button
				class="pin-button"
				class:pinned
				data-testid={pinTestid}
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

	<!-- Scrollable nav body -->
	<div class="sidebar-body">
		{@render body(expanded)}
	</div>

	<!-- Optional pinned-to-bottom section above the footer (e.g. "Return to game") -->
	{#if aside}
		<div class="sidebar-aside">
			{@render aside(expanded)}
		</div>
	{/if}

	<!-- Footer: status line -->
	<div class="sidebar-footer">
		<div class="glyph-slot">
			<div class="pulse-dot"></div>
		</div>
		{@render footer(expanded)}
	</div>
</div>

<script lang="ts">
import type { Snippet } from 'svelte';

interface Props {
	/** data-testid for the rail container. */
	testid: string;
	/** data-testid for the pin toggle button. */
	pinTestid: string;
	/** Two-way bound so the parent layout can reserve space for a pinned rail. */
	pinned?: boolean;
	/** Header height in px — the admin rail's two-line wordmark needs a slightly taller header. */
	headerHeight?: number;
	/** Vertical header padding in px. */
	headerPadding?: number;
	/** Brand markup (wordmark) shown beside the diamond glyph. Receives the expanded state. */
	brand: Snippet<[boolean]>;
	/** Nav groups/items. Receives the expanded state. */
	body: Snippet<[boolean]>;
	/** Optional section pinned above the footer. Receives the expanded state. */
	aside?: Snippet<[boolean]>;
	/** Footer status content shown beside the pulse dot. Receives the expanded state. */
	footer: Snippet<[boolean]>;
}

let {
	testid,
	pinTestid,
	pinned = $bindable(false),
	headerHeight = 58,
	headerPadding = 18,
	brand,
	body,
	aside,
	footer
}: Props = $props();

let hovering = $state(false);
let focused = $state(false);

const expanded = $derived(pinned || hovering || focused);
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
	background: var(--surface);
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
		box-shadow: 6px 0 28px color-mix(in srgb, var(--black) 55%, transparent);
	}
}

.sidebar-header {
	padding: var(--rail-header-padding) 0;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
	position: relative;
	display: flex;
	align-items: center;
	height: var(--rail-header-height);
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
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 35%, transparent);
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
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
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

.sidebar-aside {
	border-top: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
	padding: 6px 0;
}

.sidebar-footer {
	border-top: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
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
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 80%, transparent);
	animation: pulse-dot 1.6s ease-in-out infinite;
}
</style>
