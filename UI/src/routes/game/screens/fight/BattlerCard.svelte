<div class="battler-card" class:player={side === 'player'} class:enemy={side === 'enemy'} data-testid="{side}-card">
	<!-- Name + Level -->
	<div class="battler-header" class:reversed={side === 'enemy'}>
		<div class="battler-identity" class:reversed={side === 'enemy'}>
			<div class="accent-bar" style:background={accent} style:box-shadow="0 0 8px {tintColor(accent, 0.5)}"></div>
			<span class="battler-name">{battler.name}</span>
		</div>
		<span class="battler-level">LV · {battler.level}</span>
	</div>

	<!-- HP Bar -->
	<div class="hp-bar">
		<div class="hp-disappearing" style:width="{healthPerc}%"></div>
		<div class="hp-remaining" style:width="{healthPerc}%"></div>
		<div class="hp-text">{healthText}</div>
	</div>

	<!-- Active timed effects -->
	<ActiveEffectChips {battler} reversed={side === 'enemy'} />

	<!-- Skills -->
	<Skills {battler} {side} />
</div>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { type Battler } from '$lib/battle';
import { formatNum, tintColor } from '$lib/common';
import ActiveEffectChips from './ActiveEffectChips.svelte';
import Skills from './Skills.svelte';

type Props = {
	battler: Battler;
	side: 'player' | 'enemy';
};

const { battler, side }: Props = $props();

const accent = $derived(side === 'player' ? 'var(--accent)' : 'var(--enemy-accent)');
const maxHealth = $derived(battler.attributes.getValue(EAttribute.MaxHealth));
const healthText = $derived(`${formatNum(battler.currentHealth)} / ${formatNum(maxHealth)}`);
const healthPerc = $derived(maxHealth ? formatNum(Math.max((battler.currentHealth * 100) / maxHealth, 0)) : 100);
</script>

<style lang="scss">
.battler-card {
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 3px;
	padding: 18px 20px;
	color: var(--text-primary);
	width: 360px;
	min-width: 200px;
	flex-shrink: 1;

	&.player {
		border-left: 3px solid var(--accent);
		box-shadow:
			0 0 0 1px color-mix(in srgb, var(--black) 40%, transparent),
			-4px 0 18px color-mix(in srgb, var(--accent) 10%, transparent);
	}

	&.enemy {
		border-right: 3px solid var(--enemy-accent);
		box-shadow:
			0 0 0 1px color-mix(in srgb, var(--black) 40%, transparent),
			4px 0 18px color-mix(in srgb, var(--enemy-accent) 10%, transparent);
	}
}

.battler-header {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	margin-bottom: 14px;

	&.reversed {
		flex-direction: row-reverse;
	}
}

.battler-identity {
	display: flex;
	align-items: center;
	gap: 9px;

	&.reversed {
		flex-direction: row-reverse;
	}
}

.accent-bar {
	width: 4px;
	height: 18px;
}

.battler-name {
	font-size: 18px;
	font-weight: 500;
	letter-spacing: -0.1px;
}

.battler-level {
	font-family: var(--mono);
	font-size: 10.5px;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	letter-spacing: 0.6px;
}

.hp-bar {
	position: relative;
	height: 20px;
	background: var(--health-missing-color);
	border: 1px solid color-mix(in srgb, var(--white) 12%, transparent);
	border-radius: 2px;
	overflow: hidden;
	margin-bottom: 14px;
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
</style>
