<!-- The boss's HP bar: the same green-over-missing treatment as a normal battler,
     taller and overlaid with phase pips at 25 / 50 / 75% to telegraph boss phases. -->
<div
	class="boss-hp-bar"
	data-testid="boss-hp-bar"
	role="progressbar"
	aria-label="Boss health"
	aria-valuenow={Math.round(currentHealth)}
	aria-valuemin={0}
	aria-valuemax={maxHealth}
	aria-valuetext={healthText}
>
	<div class="hp-disappearing" style:width="{healthPerc}%"></div>
	<div class="hp-remaining" style:width="{healthPerc}%"></div>
	{#each PHASE_PIPS as pip (pip)}
		<div class="phase-pip" style:left="{pip}%" aria-hidden="true"></div>
	{/each}
	<div class="hp-text">{healthText}</div>
</div>

<script lang="ts">
import { formatNum } from '$lib/common';

type Props = {
	currentHealth: number;
	maxHealth: number;
};

const { currentHealth, maxHealth }: Props = $props();

const PHASE_PIPS = [25, 50, 75];
const healthText = $derived(`${formatNum(currentHealth)} / ${maxHealth}`);
const healthPerc = $derived(maxHealth ? formatNum(Math.max((currentHealth * 100) / maxHealth, 0)) : 100);
</script>

<style lang="scss">
.boss-hp-bar {
	position: relative;
	height: 22px;
	background: var(--health-missing-color);
	border: 1px solid color-mix(in srgb, var(--white) 12%, transparent);
	border-radius: 2px;
	overflow: hidden;
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
	font-size: 13px;
	font-weight: 500;
	color: var(--white);
	text-shadow: 0 1px 3px color-mix(in srgb, var(--black) 85%, transparent);
	letter-spacing: 0.4px;
}
</style>
