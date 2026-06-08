/* Presentation state for the Challenge Boss experience on the Fight screen, derived
   from the engine's boss mode (`enemyManager`), the current zone's authored boss
   (static reference data), and the per-zone `ZonesCleared` statistic. The boss
   components read this and proxy the player's actions (challenge / retreat /
   auto-fight) back to the engine, keeping `Fight.svelte` thin. */

import { IEnemy, IZone } from '$lib/api';
import { enemyManager, playerManager } from '$lib/engine';
import { staticData, statistics } from '$stores';

export class BossView {
	/** The zone the player is currently in. */
	private readonly zone = $derived<IZone | undefined>(
		staticData.zones?.find((z) => z.id === playerManager.currentZone)
	);

	/** The current zone's dedicated boss enemy, if one is authored. */
	readonly boss = $derived.by<IEnemy | undefined>(() => {
		const bossId = this.zone?.bossEnemyId;
		return bossId == null ? undefined : staticData.enemies?.[bossId];
	});

	/** Whether the current zone has a dedicated boss to challenge (gates the affordance). */
	readonly hasBoss = $derived(this.boss !== undefined);

	/** The boss's display name (falls back to a generic label before reference data loads). */
	readonly bossName = $derived(this.boss?.name ?? 'Zone Boss');

	/** The current zone's display name (the Zone-Cleared overlay's secondary line). */
	readonly zoneName = $derived(this.zone?.name ?? '');

	/** The boss's authored level for this zone (the trigger's LV badge). */
	readonly bossLevel = $derived(this.zone?.bossLevel ?? 0);

	/** Engaged in the dedicated-boss fight (the boss bar replaces the trigger). */
	readonly engaged = $derived(enemyManager.mode === 'boss');

	/** Whether this zone has been cleared at least once (drives the "Cleared" seal
	 *  and the "Re-challenge" wording). */
	readonly cleared = $derived(statistics.isZoneCleared(playerManager.currentZone));

	/** Whether the Zone-Cleared victory overlay should show. */
	readonly victory = $derived(this.engaged && enemyManager.bossOutcome === 'victory');

	/** Whether auto-fight (re-challenge on each victory) is enabled. */
	readonly autoFight = $derived(enemyManager.autoFight);

	challenge(): void {
		void enemyManager.challengeBoss();
	}

	retreat(): void {
		void enemyManager.retreatFromBoss();
	}

	toggleAutoFight(on: boolean): void {
		enemyManager.setAutoFight(on);
	}
}
