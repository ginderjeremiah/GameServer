/* statistics-mock.ts — TEMPORARY mock data for the Statistics screen.

   The backend tracks player statistics (`PlayerProgress` → `EStatisticType` +
   per-entity `PlayerStatistic` rows) and even exposes a flat `GET /api/Statistics`,
   but there is no endpoint that pairs those values with the statistic-type
   metadata and entity reference data this screen needs. Per the project
   decision, the screen is built against this self-contained mock now; wiring it
   to real backend data is tracked as a follow-up (see the issue linked in the
   PR / docs/frontend.md). Everything here is shaped like the real domain
   (`IPlayerStatistic { statisticTypeId, entityId?, value }` + entity lists that
   stand in for `staticData.enemies/zones/skills`) so the swap is mechanical:
   replace `buildMockStatistics()` with the API fetch and `MOCK_ENTITIES` with
   the live reference data.

   The raw per-entity tables are internally consistent (kills ≤ encounters,
   wins/losses, etc.) and the per-type rows are derived from them, so the
   breakdowns always agree. */

import { EStatisticType, type IPlayerStatistic } from '$lib/api';
import type { StatEntityKind } from './statistics-view.svelte';

/** A reference entity a statistic can break down by — the mock stand-in for the
 *  enemy/zone/skill reference data. */
export interface StatEntity {
	id: number;
	name: string;
	/** Boss flag (enemies only). */
	boss?: boolean;
	/** Zone order number (zones only). */
	zoneNum?: number;
}

/* ── raw per-entity tables ───────────────────────────────────────────────── */
// fastest victory times are in SECONDS, matching the backend (totalMs / 1000).
const ENEMIES = [
	{ id: 0, name: 'Cave Bat', boss: false, enc: 320, kill: 318, dmgTaken: 4200, deaths: 0, fastest: 1.8 },
	{ id: 1, name: 'Goblin Skirmisher', boss: false, enc: 210, kill: 205, dmgTaken: 9800, deaths: 1, fastest: 2.4 },
	{ id: 2, name: 'Plague Rat', boss: false, enc: 254, kill: 250, dmgTaken: 6100, deaths: 0, fastest: 2.0 },
	{ id: 3, name: 'Bone Archer', boss: false, enc: 142, kill: 138, dmgTaken: 11400, deaths: 1, fastest: 2.9 },
	{ id: 4, name: 'Crypt Hound', boss: false, enc: 166, kill: 160, dmgTaken: 13900, deaths: 2, fastest: 2.6 },
	{ id: 5, name: 'Skeleton Mage', boss: false, enc: 188, kill: 180, dmgTaken: 15200, deaths: 2, fastest: 3.1 },
	{ id: 6, name: 'Tomb Wraith', boss: false, enc: 96, kill: 88, dmgTaken: 21800, deaths: 4, fastest: 4.2 },
	{ id: 7, name: 'Stone Golem', boss: false, enc: 64, kill: 57, dmgTaken: 28600, deaths: 5, fastest: 6.8 },
	{ id: 8, name: 'Ironclad Sentinel', boss: true, enc: 22, kill: 18, dmgTaken: 33400, deaths: 4, fastest: 9.4 },
	{ id: 9, name: 'The Hollow King', boss: true, enc: 9, kill: 5, dmgTaken: 41200, deaths: 4, fastest: 14.2 }
];

const ZONES = [
	{ id: 0, name: 'Verdant Hollow', num: 1, cleared: 12 },
	{ id: 1, name: 'Sunken Crossroads', num: 2, cleared: 8 },
	{ id: 2, name: 'Forgotten Catacombs', num: 3, cleared: 5 },
	{ id: 3, name: 'Emberfall Mines', num: 4, cleared: 2 },
	{ id: 4, name: 'Frostspire Ascent', num: 5, cleared: 0 }
];

const SKILLS = [
	{ id: 0, name: 'Cleave', used: 1240, dmg: 186400, highest: 1820, heal: 0 },
	{ id: 1, name: 'Surge', used: 410, dmg: 98600, highest: 3240, heal: 0 },
	{ id: 2, name: 'Pierce', used: 680, dmg: 71200, highest: 1410, heal: 0 },
	{ id: 3, name: 'Ember Bolt', used: 520, dmg: 88200, highest: 2680, heal: 0 },
	{ id: 4, name: 'Mend', used: 360, dmg: 0, highest: 0, heal: 42800 },
	{ id: 5, name: 'Guard', used: 295, dmg: 4200, highest: 120, heal: 0 }
];

/** Mock entity reference data, keyed by entity kind (stands in for staticData). */
export const MOCK_ENTITIES: Record<StatEntityKind, StatEntity[]> = {
	enemy: ENEMIES.map((e) => ({ id: e.id, name: e.name, boss: e.boss })),
	zone: ZONES.map((z) => ({ id: z.id, name: z.name, zoneNum: z.num })),
	skill: SKILLS.map((s) => ({ id: s.id, name: s.name }))
};

const sum = (ns: number[]): number => ns.reduce((a, b) => a + b, 0);

/** Builds a faithful `IPlayerStatistic[]` from the raw tables: one per-entity
 *  row for every entity-typed statistic plus a null-entity grand total for every
 *  statistic (the shape the backend persists). */
export function buildMockStatistics(): IPlayerStatistic[] {
	const rows: IPlayerStatistic[] = [];
	const perEntity = (type: EStatisticType, entries: { id: number; value: number }[]) => {
		for (const e of entries) {
			rows.push({ statisticTypeId: type, entityId: e.id, value: e.value });
		}
	};
	const total = (type: EStatisticType, value: number) => rows.push({ statisticTypeId: type, value });

	// Enemy-typed statistics.
	perEntity(
		EStatisticType.EnemiesKilled,
		ENEMIES.map((e) => ({ id: e.id, value: e.kill }))
	);
	perEntity(
		EStatisticType.EnemiesEncountered,
		ENEMIES.map((e) => ({ id: e.id, value: e.enc }))
	);
	perEntity(
		EStatisticType.BattlesWon,
		ENEMIES.map((e) => ({ id: e.id, value: e.kill }))
	);
	perEntity(
		EStatisticType.BattlesLost,
		ENEMIES.filter((e) => e.enc - e.kill > 0).map((e) => ({ id: e.id, value: e.enc - e.kill }))
	);
	perEntity(
		EStatisticType.FastestVictory,
		ENEMIES.filter((e) => e.kill > 0).map((e) => ({ id: e.id, value: e.fastest }))
	);

	// Zone-typed statistics.
	perEntity(
		EStatisticType.ZonesCleared,
		ZONES.map((z) => ({ id: z.id, value: z.cleared }))
	);

	// Skill-typed statistics.
	perEntity(
		EStatisticType.SkillsUsed,
		SKILLS.map((s) => ({ id: s.id, value: s.used }))
	);
	perEntity(
		EStatisticType.DamageDealt,
		SKILLS.filter((s) => s.dmg > 0).map((s) => ({ id: s.id, value: s.dmg }))
	);
	perEntity(
		EStatisticType.HighestSingleAttackDamage,
		SKILLS.filter((s) => s.highest > 0).map((s) => ({ id: s.id, value: s.highest }))
	);

	// Null-entity grand totals (every statistic has one).
	total(EStatisticType.EnemiesKilled, sum(ENEMIES.map((e) => e.kill)));
	total(EStatisticType.EnemiesEncountered, sum(ENEMIES.map((e) => e.enc)));
	total(EStatisticType.BattlesWon, sum(ENEMIES.map((e) => e.kill)));
	total(EStatisticType.BattlesLost, sum(ENEMIES.map((e) => e.enc - e.kill)));
	total(EStatisticType.FastestVictory, Math.min(...ENEMIES.filter((e) => e.kill > 0).map((e) => e.fastest)));
	total(EStatisticType.ZonesCleared, sum(ZONES.map((z) => z.cleared)));
	total(EStatisticType.SkillsUsed, sum(SKILLS.map((s) => s.used)));
	total(EStatisticType.DamageDealt, sum(SKILLS.map((s) => s.dmg)));
	total(EStatisticType.HighestSingleAttackDamage, Math.max(...SKILLS.map((s) => s.highest)));
	total(EStatisticType.BossesDefeated, sum(ENEMIES.filter((e) => e.boss).map((e) => e.kill)));
	total(EStatisticType.DamageTaken, sum(ENEMIES.map((e) => e.dmgTaken)));
	total(EStatisticType.DamageHealed, sum(SKILLS.map((s) => s.heal)));
	total(EStatisticType.PlayerDeaths, sum(ENEMIES.map((e) => e.deaths)));
	total(EStatisticType.TotalBattleTime, 12420);

	return rows;
}
