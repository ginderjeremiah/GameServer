<div
	class="manifest-row"
	class:latest={isLatest}
	class:animate-in={playEntrance}
	style:height="{rowHeight}px"
	style:opacity={rowOpacity}
	style:background={isLatest
		? `linear-gradient(90deg, color-mix(in srgb, ${kind.color} 11%, transparent), color-mix(in srgb, ${kind.color} 3%, transparent) 38%, transparent 72%)`
		: undefined}
	style:box-shadow={isLatest ? `inset 2px 0 0 ${kind.color}` : undefined}
	style:--row-height="{rowHeight}px"
>
	<span class="row-time" class:latest={isLatest}>{formatLogTime(log.timestamp)}</span>

	<div
		class="row-chip"
		style:background={`color-mix(in srgb, ${kind.color} ${isLatest ? 13 : 8}%, transparent)`}
		style:border-color={`color-mix(in srgb, ${kind.color} ${isLatest ? 47 : 33}%, transparent)`}
		title={kind.label}
	>
		<LogGlyph glyph={kind.glyph} color={kind.color} size={11} />
	</div>

	<div class="row-message" class:latest={isLatest}>{log.message}</div>
</div>

<script lang="ts">
import type { LogMessage } from '$lib/engine/log';
import LogGlyph from './LogGlyph.svelte';
import { logKind, formatLogTime } from './log-kind';

interface Props {
	log: LogMessage;
	/** Position from the top (0 = newest). */
	index: number;
	isLatest: boolean;
	/** Play the slide/grow entrance. Sampled once at mount — the animation is a
	 *  mount-time effect, so later prop changes neither cancel nor replay it. */
	animate: boolean;
	rowHeight: number;
}

const { log, index, isLatest, animate, rowHeight }: Props = $props();

// Deliberately non-reactive: captures whether this row was new when it appeared.
// svelte-ignore state_referenced_locally
const playEntrance = animate;

const kind = $derived(logKind(log));
// Newest row is fully prominent; older rows fade gently so the top reads as "now".
const rowOpacity = $derived(isLatest ? 1 : Math.max(0.4, 0.9 - index * 0.1));
</script>

<style lang="scss">
.manifest-row {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 0 14px;
	overflow: hidden;

	&.animate-in {
		animation: manifest-row-in 360ms cubic-bezier(0.22, 0.61, 0.36, 1);
	}
}

.row-time {
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 0.4px;
	min-width: 54px;
	color: var(--text-muted);

	&.latest {
		color: var(--text-tertiary);
	}
}

.row-chip {
	width: 18px;
	height: 18px;
	border: 1px solid transparent;
	border-radius: 2px;
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
}

.row-message {
	flex: 1;
	min-width: 0;
	font-size: 12.5px;
	color: color-mix(in srgb, var(--text-primary) 82%, transparent);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	&.latest {
		color: color-mix(in srgb, var(--text-primary) 97%, transparent);
		font-weight: 500;
	}
}

@keyframes manifest-row-in {
	from {
		height: 0;
		opacity: 0;
	}
	to {
		height: var(--row-height);
		opacity: 1;
	}
}
</style>
