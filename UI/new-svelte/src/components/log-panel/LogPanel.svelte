<div class="log-panel" data-testid="log-panel">
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
import { untrack } from 'svelte';
import { logs } from '$stores';
import LogRow from './LogRow.svelte';

const rowHeight = 30;

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
	if (scrollEl) atTop = scrollEl.scrollTop <= 4;
};

// When a new entry arrives: if the user is pinned at the top, keep them there
// and let the new row animate in (the stack slides down). If they've scrolled
// back to read history, nudge scrollTop by one row so their view stays put.
$effect(() => {
	newestId; // track new entries
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
</script>

<style lang="scss">
.log-panel {
	border-top: 1px solid var(--border-subtle);
	background: rgba(0, 0, 0, 0.4);
	padding: 10px 8px 4px;
	position: relative;
	display: flex;
	flex-direction: column;
}

.log-header {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 0 12px 8px;
}

.log-title {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: rgba(192, 216, 255, 0.7);
}

.log-divider {
	flex: 1;
	height: 1px;
	background: rgba(240, 240, 240, 0.07);
}

.log-count {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 0.5px;
	color: rgba(240, 240, 240, 0.45);
}

.log-body {
	position: relative;
	height: 132px;
	overflow-y: auto;
	overflow-x: hidden;
	// Fade only the bottom — the newest row at the top stays fully prominent.
	mask-image: linear-gradient(to bottom, black 0%, black 78%, transparent 100%);
	-webkit-mask-image: linear-gradient(to bottom, black 0%, black 78%, transparent 100%);
	scrollbar-width: thin;
	scrollbar-color: rgba(255, 255, 255, 0.16) transparent;

	&::-webkit-scrollbar {
		width: 6px;
	}

	&::-webkit-scrollbar-thumb {
		background: rgba(255, 255, 255, 0.16);
		border-radius: 3px;
	}

	&::-webkit-scrollbar-track {
		background: transparent;
	}
}

.log-empty {
	padding: 16px 14px;
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	letter-spacing: 0.5px;
	color: rgba(240, 240, 240, 0.35);
}
</style>
