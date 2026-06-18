<!-- Shared HP bar: the green-over-missing health treatment used by both the normal
     BattlerCard and the boss card. The `tall` variant raises the height/text weight for
     the boss, and `phasePips` overlays phase markers (e.g. 25/50/75%). -->
<div
	class="hp-bar"
	class:tall
	data-testid={testId}
	role="progressbar"
	aria-label={ariaLabel}
	aria-valuenow={Math.round(currentHealth)}
	aria-valuemin={0}
	aria-valuemax={maxHealth}
	aria-valuetext={healthText}
>
	<div class="hp-disappearing" style:width="{healthPerc}%"></div>
	<div class="hp-remaining" style:width="{healthPerc}%"></div>
	{#each phasePips as pip (pip)}
		<div class="phase-pip" style:left="{pip}%" aria-hidden="true"></div>
	{/each}
	<div class="hp-text">{healthText}</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';

type Props = {
	currentHealth: number;
	maxHealth: number;
	/** Accessible name for the bar (e.g. "Aelara health"). */
	ariaLabel: string;
	/** Taller bar with bolder text, used by the boss card. */
	tall?: boolean;
	/** Percentages (0–100) to overlay as phase markers. */
	phasePips?: number[];
	testId?: string;
};

const { currentHealth, maxHealth, ariaLabel, tall = false, phasePips = [], testId }: Props = $props();

const healthText = $derived(`${formatNum(currentHealth)} / ${formatNum(maxHealth)}`);
// Bar fill geometry is a clamped number (0–100), independent of the display formatter — a transient
// currentHealth > maxHealth can't overflow the bar, and a zero/NaN maxHealth resolves to full.
const healthPerc = $derived(maxHealth ? Math.min(100, Math.max(0, (currentHealth * 100) / maxHealth)) : 100);
</script>

<style lang="scss">
.hp-bar {
	position: relative;
	height: 20px;
	background: var(--health-missing-color);
	border: 1px solid color-mix(in srgb, var(--white) 12%, transparent);
	border-radius: 2px;
	overflow: hidden;

	&.tall {
		height: 22px;
	}
}

.hp-disappearing {
	position: absolute;
	inset: 0;
	background: var(--health-disappearing-color);
	transition: width 1s ease-out;
}

.hp-remaining {
	position: absolute;
	inset: 0;
	background: linear-gradient(180deg, var(--health-remaining-color) 0%, var(--health-remaining-dark) 100%);
	transition: width 120ms ease-out;
}

.phase-pip {
	position: absolute;
	top: 0;
	bottom: 0;
	width: 1px;
	background: color-mix(in srgb, var(--black) 45%, transparent);
	box-shadow: 0 0 0 0.5px color-mix(in srgb, var(--boss-accent) 20%, transparent);
}

.hp-text {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--white);
	text-shadow: 0 1px 2px color-mix(in srgb, var(--black) 70%, transparent);
	letter-spacing: 0.3px;
}

.tall .hp-text {
	font-size: 13px;
	font-weight: 500;
	text-shadow: 0 1px 3px color-mix(in srgb, var(--black) 85%, transparent);
	letter-spacing: 0.4px;
}
</style>
