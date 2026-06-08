<div
	class="log-panel"
	class:resizing={view.dragging}
	data-testid="log-panel"
	style:height="{view.height}px"
	bind:this={panelEl}
>
	<div
		class="log-resize-handle"
		role="separator"
		aria-orientation="horizontal"
		aria-label="Resize combat log"
		data-testid="log-resize-handle"
		onpointerdown={onHandleDown}
	></div>

	<div class="log-header">
		<span class="log-title">Combat Log</span>
		<div class="log-divider"></div>
		<span class="log-count">{eventCount} events</span>
	</div>

	<div class="log-body" bind:this={scrollEl} onscroll={onScroll}>
		{#each allLogs as log, i (log.id)}
			<LogRow {log} index={i} isLatest={i === 0} animate={i === 0 && isNew && atTop} {rowHeight} />
		{/each}
		{#if allLogs.length === 0}
			<div class="log-empty">No combat activity yet.</div>
		{/if}
	</div>
</div>

<script lang="ts">
import { untrack, onMount } from 'svelte';
import { logs } from '$stores';
import LogRow from './LogRow.svelte';
import { LogPanelView } from './log-panel-view.svelte';

const rowHeight = 30;

// Drives the draggable panel height (see log-panel-view for the clamping/persistence).
const view = new LogPanelView();
let panelEl: HTMLDivElement | undefined;

// The panel shares `.main-content` with the screen above it; that container's
// height caps how far the log can grow.
const availableHeight = () => panelEl?.parentElement?.clientHeight;

const onHandleDown = (e: PointerEvent) => {
	e.preventDefault();
	view.beginResize(e.clientY, availableHeight() ?? view.height);
	document.body.classList.add('log-resizing');
};

// Logs are stored newest-first (unshifted), so index 0 is the newest entry.
const allLogs = $derived(logs());
const newestId = $derived(allLogs[0]?.id ?? 0);
const eventCount = $derived(allLogs.length ? newestId : 0);

let scrollEl: HTMLDivElement | undefined;
// `atTop` is read inside the effect via `untrack` so scroll changes don't
// re-run the pinning effect — we only want it to react to new entries.
let atTop = $state(true);
// Tracks the id we've already processed, so the entrance animation plays only
// for a freshly-arrived top row — not every time the list re-renders.
let seenId = $state(0);
const isNew = $derived(newestId !== seenId);

const onScroll = () => {
	if (scrollEl) {
		atTop = scrollEl.scrollTop <= 4;
	}
};

// When a new entry arrives: if the user is pinned at the top, keep them there
// and let the new row animate in (the stack slides down). If they've scrolled
// back to read history, nudge scrollTop by one row so their view stays put.
$effect(() => {
	const el = scrollEl;
	if (el) {
		if (untrack(() => atTop)) {
			el.scrollTop = 0;
		} else {
			el.scrollTop += rowHeight;
		}
	}
	seenId = newestId; // mark this entry consumed (after positioning)
});

onMount(() => {
	// Restore the persisted height after mount so SSR markup and the first client
	// render agree (no hydration mismatch on the inline height). Clamp it against
	// the live container so a height saved on a larger viewport can't overflow here.
	view.hydrate(availableHeight());

	// The gesture is tracked on `window` so it keeps following the pointer once it
	// leaves the thin handle (mirrors the card-game board's drag).
	const onMove = (e: PointerEvent) => {
		view.moveResize(e.clientY);
	};
	const onUp = () => {
		if (view.dragging) {
			view.endResize();
			document.body.classList.remove('log-resizing');
		}
	};
	// Shrinking the window can leave the log taller than the space it shares with
	// the screen — re-clamp so it stays within bounds without needing a drag.
	const onResize = () => {
		const available = availableHeight();
		if (available !== undefined) {
			view.clampToAvailable(available);
		}
	};
	window.addEventListener('pointermove', onMove);
	window.addEventListener('pointerup', onUp);
	window.addEventListener('resize', onResize);
	return () => {
		window.removeEventListener('pointermove', onMove);
		window.removeEventListener('pointerup', onUp);
		window.removeEventListener('resize', onResize);
		document.body.classList.remove('log-resizing');
	};
});
</script>

<style lang="scss">
.log-panel {
	border-top: 1px solid var(--border-subtle);
	background: color-mix(in srgb, var(--black) 40%, transparent);
	padding: 10px 8px 4px;
	position: relative;
	display: flex;
	flex-direction: column;
	// Height is driven by the resize view-model via an inline style; never let the
	// flex column squeeze the panel below it.
	flex-shrink: 0;
	min-height: 0;
}

// Thin grab strip straddling the panel's top edge — the ↕ resize affordance.
.log-resize-handle {
	position: absolute;
	top: -4px;
	left: 0;
	right: 0;
	height: 9px;
	z-index: 2;
	cursor: ns-resize;
	touch-action: none;

	&::after {
		content: '';
		position: absolute;
		left: 0;
		right: 0;
		top: 4px;
		height: 1px;
		background: transparent;
		transition: background 120ms ease;
	}

	&:hover::after {
		background: color-mix(in srgb, var(--accent) 55%, transparent);
	}
}

.log-panel.resizing .log-resize-handle::after {
	background: var(--accent);
}

.log-header {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 0 12px 8px;
}

.log-title {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: var(--eyebrow);
}

.log-divider {
	flex: 1;
	height: 1px;
	background: color-mix(in srgb, var(--text-primary) 7%, transparent);
}

.log-count {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.5px;
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
}

.log-body {
	position: relative;
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	overflow-x: hidden;
	// Fade only the bottom — the newest row at the top stays fully prominent.
	mask-image: linear-gradient(to bottom, black 0%, black 78%, transparent 100%);
	-webkit-mask-image: linear-gradient(to bottom, black 0%, black 78%, transparent 100%);
	scrollbar-width: thin;
	scrollbar-color: color-mix(in srgb, var(--white) 16%, transparent) transparent;

	&::-webkit-scrollbar {
		width: 6px;
	}

	&::-webkit-scrollbar-thumb {
		background: color-mix(in srgb, var(--white) 16%, transparent);
		border-radius: 3px;
	}

	&::-webkit-scrollbar-track {
		background: transparent;
	}
}

.log-empty {
	padding: 16px 14px;
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	color: color-mix(in srgb, var(--text-primary) 35%, transparent);
}
</style>
