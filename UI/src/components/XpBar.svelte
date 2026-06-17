<!-- Player experience bar: a slim gold track whose fill grows with progress toward the next level,
     with a travelling specular sweep and a gold flash on level-up. A multi-layer/animated bar (the
     shimmer is clipped to the fill), so like HpBar it stays specialised rather than building on the
     generic Bar. Sized by the consumer — width fills its container, height via `--xp-bar-height`. -->
<div
	class="xp-bar"
	data-testid={testId}
	role="progressbar"
	aria-label={ariaLabel}
	aria-valuenow={Math.round(exp)}
	aria-valuemin={0}
	aria-valuemax={Math.round(nextLevelThreshold)}
	aria-valuetext={valueText}
>
	<div class="xp-fill" bind:this={fillEl} style:width="{fillPercent}%">
		<div class="xp-shimmer"></div>
	</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';

type Props = {
	/** Current player level, watched to flash the bar when it increases. */
	level: number;
	/** Experience accumulated toward the next level (already net of past levels). */
	exp: number;
	/** Experience needed to reach the next level. */
	nextLevelThreshold: number;
	ariaLabel?: string;
	testId?: string;
};

const { level, exp, nextLevelThreshold, ariaLabel = 'Experience', testId }: Props = $props();

const fillPercent = $derived(nextLevelThreshold > 0 ? Math.min(100, Math.max(0, (exp / nextLevelThreshold) * 100)) : 0);
const valueText = $derived(`${formatNum(exp)} / ${formatNum(nextLevelThreshold)} XP`);

let fillEl = $state<HTMLDivElement>();
// Re-trigger the level-up flash by clearing and re-applying the animation whenever the level rises.
// `lastLevel` starts undefined so the first run only records the baseline (no flash on mount).
let lastLevel: number | undefined;
$effect(() => {
	const current = level;
	if (lastLevel !== undefined && current > lastLevel && fillEl) {
		fillEl.style.animation = 'none';
		void fillEl.offsetWidth;
		fillEl.style.animation = 'levelup-flash 900ms ease-out';
	}
	lastLevel = current;
});
</script>

<style lang="scss">
.xp-bar {
	position: relative;
	width: 100%;
	height: var(--xp-bar-height, 4px);
	border-radius: 2px;
	background: color-mix(in srgb, var(--white) 7%, transparent);
	overflow: hidden;
}

.xp-fill {
	position: absolute;
	inset: 0;
	border-radius: inherit;
	background: var(--gold);
	transition: width 0.35s ease-out;
	overflow: hidden;
}

.xp-shimmer {
	position: absolute;
	top: 0;
	bottom: 0;
	left: 0;
	width: 24px;
	background: linear-gradient(90deg, transparent, color-mix(in srgb, var(--white) 50%, transparent), transparent);
	animation: xp-shimmer 2.8s ease-in-out infinite;
}
</style>
