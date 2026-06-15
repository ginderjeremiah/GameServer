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
	<div class="hp-bar-slot">
		<HpBar currentHealth={battler.currentHealth} {maxHealth} ariaLabel="{battler.name} health" />
	</div>

	<!-- Skills -->
	<Skills {battler} {side} />

	<!-- Active timed effects float below the card (absolutely positioned) so effects coming and
	     going never change the card's height — which would shift the vertically-centred combatants row. -->
	<div class="effect-chips-slot">
		<ActiveEffectChips {battler} reversed={side === 'enemy'} />
	</div>
</div>

<script lang="ts">
import { EAttribute } from '$lib/api';
import { type Battler } from '$lib/battle';
import { tintColor } from '$lib/common';
import { HpBar } from '$components';
import ActiveEffectChips from './ActiveEffectChips.svelte';
import Skills from './Skills.svelte';

type Props = {
	battler: Battler;
	side: 'player' | 'enemy';
};

const { battler, side }: Props = $props();

const accent = $derived(side === 'player' ? 'var(--accent)' : 'var(--enemy-accent)');
const maxHealth = $derived(battler.attributes.getValue(EAttribute.MaxHealth));
</script>

<style lang="scss">
.battler-card {
	position: relative;
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

.hp-bar-slot {
	margin-bottom: 14px;
}

// Anchored to the card's bottom edge and inset to the content padding, so the effect tiles line up
// under the skill row without occupying card height.
.effect-chips-slot {
	position: absolute;
	top: 100%;
	left: 20px;
	right: 20px;
	padding-top: 12px;
}
</style>
