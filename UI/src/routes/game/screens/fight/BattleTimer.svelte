<!-- The battle clock that sits above the VS badge: a stacked mono readout of elapsed time against the
     2:00 limit, with a thin progress track that turns to the warning hue in the final seconds. At the
     cap the battle is a timeout (a draw — the idle farm continues), shown as TIMEOUT · draw. -->
<div class="battle-timer" data-testid="battle-timer">
	{#if timedOut}
		<div class="readout timeout" data-testid="battle-timer-timeout">TIMEOUT</div>
		<div class="caption">draw</div>
	{:else}
		<div class="readout">{elapsedText}</div>
		<div class="caption">/ {limitText} limit</div>
	{/if}
	<div class="track">
		<div class="track-fill" class:warning style:width="{fillPercent}%"></div>
	</div>
</div>

<script lang="ts">
type Props = {
	/** Elapsed battle time in milliseconds (the engine's `timeElapsed`). */
	elapsedMs: number;
	/** The battle's maximum duration; reaching it is a timeout/draw. */
	maxMs: number;
};

const { elapsedMs, maxMs }: Props = $props();

/** How long before the cap the track flips to the warning hue (the final 20 seconds). */
const WARNING_WINDOW_MS = 20000;

const timedOut = $derived(elapsedMs >= maxMs);
const warning = $derived(elapsedMs >= maxMs - WARNING_WINDOW_MS);
const fillPercent = $derived(maxMs > 0 ? Math.min(100, (elapsedMs / maxMs) * 100) : 0);

const formatClock = (ms: number): string => {
	const totalSeconds = Math.floor(ms / 1000);
	const minutes = Math.floor(totalSeconds / 60);
	const seconds = totalSeconds % 60;
	return `${minutes}:${String(seconds).padStart(2, '0')}`;
};

// Cap the readout at the limit so a final partial tick never shows 2:01.
const elapsedText = $derived(formatClock(Math.min(elapsedMs, maxMs)));
const limitText = $derived(formatClock(maxMs));
</script>

<style lang="scss">
.battle-timer {
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 4px;
	min-width: 124px;
}

.readout {
	font-family: var(--mono);
	font-weight: 500;
	font-size: 28px;
	letter-spacing: 1.5px;
	line-height: 1;
	color: var(--text-primary);

	&.timeout {
		font-weight: 600;
		font-size: 20px;
		letter-spacing: 2.5px;
		color: var(--enemy-accent);
	}
}

.caption {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.track {
	width: 120px;
	height: 2px;
	margin-top: 3px;
	border-radius: 1px;
	background: color-mix(in srgb, var(--white) 9%, transparent);
	overflow: hidden;
}

.track-fill {
	height: 100%;
	background: var(--accent);
	transition:
		width 0.12s linear,
		background 0.4s;

	&.warning {
		background: var(--warning);
	}
}
</style>
