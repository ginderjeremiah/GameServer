<div class="fight-screen" data-testid="fight-screen">
	{#if boss.engaged}
		<BossAtmosphere />
	{/if}

	<div class="zone-nav-wrapper">
		<ZoneNav />
	</div>

	{#if boss.hasBoss}
		<div class="boss-affordance-wrapper">
			<div class="boss-affordance-inner">
				<BossAffordanceSlot view={boss} />
			</div>
		</div>
	{/if}

	<div class="combatants">
		<BattlerCard battler={battleEngine.player} side="player" />

		<!-- Center column: the battle status readout rides above the VS badge, clear of the zone-nav/boss
		     band. Between enemies (the Loading cooldown) it swaps the battle clock for the next-enemy
		     countdown; otherwise it shows the elapsed-vs-limit battle timer. -->
		<div class="center-column">
			{#if battleEngine.stage === BattleStage.Loading}
				<EnemyCooldown remainingMs={battleEngine.loadingTime} totalMs={battleEngine.loadingTotal} />
			{:else}
				<BattleTimer elapsedMs={battleEngine.timeElapsed} maxMs={DEFAULT_MAX_BATTLE_MS} />
			{/if}
			<div class="vs-badge" class:boss={boss.engaged} data-testid="vs-badge">
				<div class="vs-diamond"></div>
				<div class="vs-diamond-inner"></div>
				<span class="vs-text">vs</span>
			</div>
		</div>

		{#if boss.engaged}
			<BossBattlerCard battler={battleEngine.enemy} />
		{:else}
			<BattlerCard battler={battleEngine.enemy} side="enemy" />
		{/if}
	</div>

	{#if boss.victory}
		<ZoneClearedOverlay
			bossName={boss.bossName}
			zoneName={boss.zoneName}
			autoFight={boss.autoFight}
			unlockedNextZone={boss.unlockedNextZone}
		/>
	{/if}
</div>

<script lang="ts">
import ZoneNav from './ZoneNav.svelte';
import BattlerCard from './BattlerCard.svelte';
import BattleTimer from './BattleTimer.svelte';
import EnemyCooldown from './EnemyCooldown.svelte';
import BossAffordanceSlot from './boss/BossAffordanceSlot.svelte';
import BossBattlerCard from './boss/BossBattlerCard.svelte';
import BossAtmosphere from './boss/BossAtmosphere.svelte';
import ZoneClearedOverlay from './boss/ZoneClearedOverlay.svelte';
import { BossView } from './boss/boss-view.svelte';
import { battleEngine, BattleStage } from '$lib/engine';
import { DEFAULT_MAX_BATTLE_MS } from '$lib/api/types/game-constants';

const boss = new BossView();
</script>

<style lang="scss">
.fight-screen {
	width: 100%;
	height: 100%;
	position: relative;
	display: flex;
	flex-direction: column;
	overflow: auto;
}

.zone-nav-wrapper {
	position: relative;
	z-index: 1;
	padding: 22px 32px 0;
	display: flex;
	justify-content: center;
}

.boss-affordance-wrapper {
	position: relative;
	z-index: 2;
	padding: 12px 32px 0;
	display: flex;
	justify-content: center;
}

.boss-affordance-inner {
	width: 100%;
	max-width: 720px;
}

.combatants {
	flex: 1;
	position: relative;
	z-index: 1;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 40px;
	padding: 20px 36px 96px;
	min-height: 0;
}

.center-column {
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 30px;
	flex-shrink: 0;
}

.vs-badge {
	width: 56px;
	height: 56px;
	position: relative;
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
}

.vs-diamond {
	position: absolute;
	inset: 0;
	border: 1px solid var(--border-medium);
	transform: rotate(45deg);
	background: color-mix(in srgb, var(--surface) 60%, transparent);
}

.vs-diamond-inner {
	position: absolute;
	inset: 14px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	transform: rotate(45deg);
	box-shadow: inset 0 0 12px var(--border-subtle);
}

.vs-text {
	position: relative;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 85%, transparent);
}

// Gold "versus" treatment while a boss is engaged.
.vs-badge.boss {
	.vs-diamond {
		border-color: color-mix(in srgb, var(--boss-accent) 33%, transparent);
	}

	.vs-diamond-inner {
		inset: 12px;
		background: none;
		border: 1px solid color-mix(in srgb, var(--boss-accent) 53%, transparent);
		box-shadow: inset 0 0 12px color-mix(in srgb, var(--boss-accent) 20%, transparent);
	}

	.vs-text {
		color: var(--boss-accent);
	}
}
</style>
