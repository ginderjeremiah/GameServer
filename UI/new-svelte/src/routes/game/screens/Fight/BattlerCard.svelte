<div class="battler-card" class:player={side === 'player'} class:enemy={side === 'enemy'} data-testid="{side}-card">
	<!-- Name + Level -->
	<div class="battler-header" class:reversed={side === 'enemy'}>
		<div class="battler-identity" class:reversed={side === 'enemy'}>
			<div class="accent-bar" style:background={accent}
				style:box-shadow="0 0 8px {accent}80"></div>
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

	<!-- Skills -->
	<Skills {battler} {side} />
</div>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { type Battler } from '$lib/battle';
import { formatNum } from '$lib/common';
import Skills from './Skills.svelte';

type Props = {
	battler: Battler;
	side: 'player' | 'enemy';
};

const { battler, side }: Props = $props();

const accent = $derived(side === 'player' ? '#a1c2f7' : '#e08778');
const maxHealth = $derived(battler.attributes.getValue(EAttribute.MaxHealth));
const healthText = $derived(`${formatNum(battler.currentHealth)} / ${maxHealth}`);
const healthPerc = $derived(
	maxHealth ? formatNum(Math.max((battler.currentHealth * 100) / maxHealth, 0)) : 100
);
</script>

<style lang="scss">
.battler-card {
	background: rgba(255, 255, 255, 0.03);
	border: 1px solid rgba(255, 255, 255, 0.14);
	border-radius: 3px;
	padding: 18px 20px;
	color: #f0f0f0;
	width: 360px;
	min-width: 200px;
	flex-shrink: 1;

	&.player {
		border-left: 3px solid #a1c2f7;
		box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.4), -4px 0 18px rgba(161, 194, 247, 0.1);
	}

	&.enemy {
		border-right: 3px solid #e08778;
		box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.4), 4px 0 18px rgba(224, 135, 120, 0.1);
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
	font-family: 'Geist Mono', monospace;
	font-size: 10.5px;
	color: rgba(240, 240, 240, 0.6);
	letter-spacing: 0.6px;
}

.hp-bar {
	position: relative;
	height: 20px;
	background: rgba(224, 138, 120, 0.18);
	border: 1px solid rgba(255, 255, 255, 0.12);
	border-radius: 2px;
	overflow: hidden;
	margin-bottom: 14px;
}

.hp-disappearing {
	position: absolute;
	inset: 0;
	background: rgba(224, 138, 120, 0.55);
	transition: width 1s ease-out;
}

.hp-remaining {
	position: absolute;
	inset: 0;
	background: linear-gradient(180deg, #7fc28b 0%, #5da66a 100%);
	transition: width 120ms ease-out;
}

.hp-text {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: 'Geist Mono', monospace;
	font-size: 11px;
	color: #fff;
	text-shadow: 0 1px 2px rgba(0, 0, 0, 0.7);
	letter-spacing: 0.3px;
}
</style>
