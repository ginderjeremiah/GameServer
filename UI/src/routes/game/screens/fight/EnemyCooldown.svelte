<!-- The between-enemies cooldown readout, shown in the center column in place of the battle clock while
     the fight is in the Loading stage (an enemy was just defeated/lost and the next is on its way). A
     mono seconds countdown over a track that fills as the next foe approaches, themed in the enemy hue
     to read as "incoming". -->
<div class="enemy-cooldown" data-testid="enemy-cooldown">
	<div class="readout">{secondsText}</div>
	<div class="caption">next enemy</div>
	<div class="track">
		<div class="track-fill" style:width="{fillPercent}%"></div>
	</div>
</div>

<script lang="ts">
type Props = {
	/** Remaining cooldown in milliseconds (the engine's `loadingTime`). */
	remainingMs: number;
	/** The full cooldown duration the countdown started from (the engine's `loadingTotal`). */
	totalMs: number;
};

const { remainingMs, totalMs }: Props = $props();

// Round up so the readout counts whole seconds down to 1 (a fractional final second still reads "1s"),
// clamped at 0 so a tiny negative overshoot never shows "-0s".
const secondsLeft = $derived(Math.max(0, Math.ceil(remainingMs / 1000)));
const secondsText = $derived(`${secondsLeft}s`);

// The track fills as the cooldown elapses (0% → 100%), so it reads as a foe loading in. Guard a zero
// total (no countdown yet) against a divide-by-zero.
const fillPercent = $derived(totalMs > 0 ? Math.min(100, Math.max(0, ((totalMs - remainingMs) / totalMs) * 100)) : 0);
</script>

<style lang="scss">
.enemy-cooldown {
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
	color: var(--enemy-accent);
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
	background: var(--enemy-accent);
	transition: width 0.12s linear;
}
</style>
