/* Pure level-band + spawn-share maths for the Codex enemy views. Kept free of
   reactive state (and the store) so it can be unit-tested directly and shared by
   the enemy table, the dossier, and the future Zones tab. */

import type { IEnemy, IZone } from '$lib/api';

export interface LevelRange {
	min: number;
	max: number;
	/** A boss is a fixed-level encounter (min === max). */
	fixed: boolean;
}

/** An enemy's level band: a boss is fixed at its zone's `bossLevel`; a normal enemy spans the
 *  min/max levels of the zones it spawns in. Zones are resolved by id (the zero-based convention);
 *  an enemy with no resolvable spawn zone degrades to level 1. */
export function levelRange(enemy: IEnemy, zones: IZone[]): LevelRange {
	if (enemy.isBoss) {
		const zone = zones.find((z) => z?.bossEnemyId === enemy.id);
		const level = zone?.bossLevel ?? 1;
		return { min: level, max: level, fixed: true };
	}
	const spawnZones = enemy.spawns.map((s) => zones[s.zoneId]).filter(Boolean);
	if (spawnZones.length === 0) {
		return { min: 1, max: 1, fixed: false };
	}
	return {
		min: Math.min(...spawnZones.map((z) => z.levelMin)),
		max: Math.max(...spawnZones.map((z) => z.levelMax)),
		fixed: false
	};
}

/** Total spawn weight across every enemy that spawns in a zone — the denominator for a spawn share. */
export function zoneTotalWeight(zoneId: number, enemies: IEnemy[]): number {
	return enemies.reduce((sum, e) => sum + (e.spawns.find((s) => s.zoneId === zoneId)?.weight ?? 0), 0);
}

/** A spawn's share of its zone as a 0–100 percentage (0 when the zone has no weight). */
export function spawnShare(weight: number, total: number): number {
	return total > 0 ? Math.round((weight / total) * 100) : 0;
}
