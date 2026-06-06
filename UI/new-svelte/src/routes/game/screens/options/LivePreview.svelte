<div class="preview" data-testid="live-preview">
	<div class="preview-header">
		<span class="pulse-dot"></span>
		<span class="preview-eyebrow">Live Preview</span>
		<div class="preview-divider"></div>
		<span class="preview-tag">Combat Log</span>
	</div>

	<div class="preview-body">
		{#if shown.length === 0}
			<div class="preview-empty">All log types are disabled —<br />the combat log stays empty.</div>
		{:else}
			{#each shown as log, i (log.id)}
				<LogRow {log} index={i} isLatest={i === 0} animate={i === 0 && log.id === latestId} rowHeight={28} />
			{/each}
		{/if}
	</div>
</div>

<script lang="ts">
import { LogRow } from '$components';
import type { LogMessage } from '$lib/engine/log';
import { ELogType } from '$lib/api';
import type { LogPrefMap } from './options-view.svelte';

interface Props {
	/** The draft preference map — the preview shows what these toggles produce. */
	prefs: LogPrefMap;
}

const { prefs }: Props = $props();

/* Self-contained sample feed across every real log type, so toggling any row
   visibly changes the output. Messages follow the battle engine's phrasing
   conventions (a leading "You" marks player actions) so `logKind` tints them
   exactly like the real combat log. */
const SAMPLES: { logType: ELogType; messages: string[] }[] = [
	{
		logType: ELogType.Damage,
		messages: ['You cast Cleave on the Skeleton Mage.', 'The Skeleton Mage hits you for 14.']
	},
	{ logType: ELogType.EnemyDefeated, messages: ['The Skeleton Mage was defeated.'] },
	{ logType: ELogType.ItemFound, messages: ['Found a Sapphire Shard.'] },
	{ logType: ELogType.Exp, messages: ['Earned 47 exp.'] },
	{ logType: ELogType.LevelUp, messages: ['Congratulations, you leveled up!'] },
	{ logType: ELogType.Debug, messages: ['battle.tick resolved in 4ms.'] }
];

// Weighted so the common combat events dominate the feed, like a real battle.
const WEIGHTS: ELogType[] = [
	ELogType.Damage,
	ELogType.Damage,
	ELogType.Damage,
	ELogType.ItemFound,
	ELogType.Exp,
	ELogType.EnemyDefeated,
	ELogType.LevelUp,
	ELogType.Debug
];

const PREVIEW_BUFFER = 24;
const PREVIEW_VISIBLE = 9;
const TICK_MS = 1600;

let nextId = 1;

function makeEvent(): LogMessage {
	const logType = WEIGHTS[Math.floor(Math.random() * WEIGHTS.length)];
	const sample = SAMPLES.find((s) => s.logType === logType)!;
	const message = sample.messages[Math.floor(Math.random() * sample.messages.length)];
	return { id: nextId++, logType, message };
}

// Stored oldest → newest; rendered newest-first to mirror the real LogPanel.
let events = $state<LogMessage[]>(Array.from({ length: PREVIEW_VISIBLE }, makeEvent));

const latestId = $derived(events[events.length - 1]?.id ?? 0);
const shown = $derived(
	events
		.filter((e) => prefs[e.logType])
		.slice(-PREVIEW_VISIBLE)
		.reverse()
);

$effect(() => {
	const timer = setInterval(() => {
		events = [...events.slice(-(PREVIEW_BUFFER - 1)), makeEvent()];
	}, TICK_MS);
	return () => clearInterval(timer);
});
</script>

<style lang="scss">
.preview {
	display: flex;
	flex-direction: column;
	min-height: 0;
	height: 100%;
	width: 100%;
	background: color-mix(in srgb, var(--black) 28%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	overflow: hidden;
}

.preview-header {
	display: flex;
	align-items: center;
	gap: 9px;
	padding: 12px 16px;
	border-bottom: 1px solid color-mix(in srgb, var(--white) 6%, transparent);
}

.pulse-dot {
	width: 6px;
	height: 6px;
	border-radius: 50%;
	background: var(--accent);
	box-shadow: 0 0 8px var(--accent);
	animation: op-pulse 1.6s ease-in-out infinite;
}

.preview-eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--eyebrow);
}

.preview-divider {
	flex: 1;
	height: 1px;
	background: color-mix(in srgb, var(--text-primary) 6%, transparent);
}

.preview-tag {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.5px;
	color: var(--text-muted);
}

.preview-body {
	flex: 1;
	min-height: 0;
	overflow: hidden;
	padding: 8px 4px;
	display: flex;
	flex-direction: column;
	justify-content: flex-end;
}

.preview-empty {
	flex: 1;
	display: flex;
	align-items: center;
	justify-content: center;
	text-align: center;
	padding: 20px;
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.5px;
	color: color-mix(in srgb, var(--text-primary) 35%, transparent);
}

@keyframes op-pulse {
	0%,
	100% {
		opacity: 0.6;
	}
	50% {
		opacity: 1;
	}
}
</style>
