<div class="battler-card" class:player={side === 'player'} class:enemy={side === 'enemy'} data-testid="{side}-card">
	<CombatFloaters {side} testId="{side}-floaters" />

	<!-- Name + Level -->
	<div class="battler-header" class:reversed={side === 'enemy'} class:player={side === 'player'}>
		<div class="battler-identity" class:reversed={side === 'enemy'}>
			<div class="accent-bar" style:background={accent} style:box-shadow="0 0 8px {tintColor(accent, 0.5)}"></div>
			<span class="battler-name">{battler.name}</span>
		</div>
		{#if side === 'player'}
			<!-- The player's level + XP progress toward the next level (gold track). -->
			<div class="player-progress">
				<div class="level-row">
					<span class="player-level">LV {playerManager.level}</span>
					<span class="power-readout" data-testid="player-power"
						>⚡ {formatNum(Math.round(playerManager.playerRating))}</span
					>
				</div>
				<div class="xp-bar-slot">
					<XpBar
						level={playerManager.level}
						exp={playerManager.exp}
						nextLevelThreshold={playerManager.nextLevelThreshold}
						ariaLabel="{battler.name} experience"
						testId="player-xp-bar"
					/>
				</div>
			</div>
		{:else}
			<div class="enemy-progress">
				<span class="battler-level">LV · {battler.level}</span>
				{#if enemyManager.currentEnemy}
					<!-- The matched/trivial cue makes the anti-grind curve legible ("you've outgrown this
					     zone") instead of a bare number (spike #1526 Decision 7). -->
					<span class="power-readout" data-testid="enemy-power" style:color="var({ratingCueColorVar(cue)})">
						⚡ {formatNum(Math.round(enemyManager.currentEnemy.enemyRating))} · {ratingCueLabel(cue)}
					</span>
				{/if}
			</div>
		{/if}
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
import { formatNum, tintColor } from '$lib/common';
import { enemyManager, playerManager } from '$lib/engine';
import { HpBar, XpBar } from '$components';
import ActiveEffectChips from './ActiveEffectChips.svelte';
import CombatFloaters from './CombatFloaters.svelte';
import Skills from './Skills.svelte';
import { ratingCue, ratingCueColorVar, ratingCueLabel } from './rating-cue';

type Props = {
	battler: Battler;
	side: 'player' | 'enemy';
};

const { battler, side }: Props = $props();

const accent = $derived(side === 'player' ? 'var(--accent)' : 'var(--enemy-accent)');
const maxHealth = $derived(battler.attributes.getValue(EAttribute.MaxHealth));
const cue = $derived(ratingCue(enemyManager.currentEnemy?.enemyRating ?? 0, playerManager.playerRating));
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

	// The player's XP column is taller than the name, so bottom-align the row.
	&.player {
		align-items: flex-end;
	}
}

.player-progress {
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	gap: 3px;
}

.level-row {
	display: flex;
	align-items: baseline;
	gap: 8px;
}

.enemy-progress {
	display: flex;
	flex-direction: column;
	align-items: flex-start;
	gap: 3px;
}

.player-level {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--accent-light);
	letter-spacing: 0.8px;
}

// The combat-power readout (spike #1526 Decision 7): a shared style for both sides, with the enemy's
// colour driven inline by its matched/trivial cue (see rating-cue.ts).
.power-readout {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.6px;
	color: var(--accent-light);
	white-space: nowrap;
}

.xp-bar-slot {
	width: 104px;
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
