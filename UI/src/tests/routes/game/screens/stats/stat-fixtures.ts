/* Shared fixtures for the Statistics screen tests. Not a test file (no
   .test/.spec suffix) so vitest does not collect it. */

import { EEntityType, EStatisticType, type IStatisticType } from '$lib/api';

/** The statistic-type reference data the backend exposes — mirrors
 *  `StatisticType.GetEntityType` + the `SpaceWords()` display names. */
export const SERVER_STAT_TYPES: IStatisticType[] = [
	{ id: EStatisticType.EnemiesKilled, entityType: EEntityType.Enemy, name: 'Enemies Killed' },
	{ id: EStatisticType.BossesDefeated, entityType: EEntityType.Enemy, name: 'Bosses Defeated' },
	{ id: EStatisticType.ZonesCleared, entityType: EEntityType.Zone, name: 'Zones Cleared' },
	{ id: EStatisticType.DamageDealt, entityType: EEntityType.Skill, name: 'Damage Dealt' },
	{ id: EStatisticType.HighestSingleAttackDamage, entityType: EEntityType.Skill, name: 'Highest Single Attack Damage' },
	{ id: EStatisticType.DamageTaken, entityType: EEntityType.None, name: 'Damage Taken' },
	{ id: EStatisticType.DamageHealed, entityType: EEntityType.None, name: 'Damage Healed' },
	{ id: EStatisticType.EnemiesEncountered, entityType: EEntityType.Enemy, name: 'Enemies Encountered' },
	{ id: EStatisticType.BattlesWon, entityType: EEntityType.Enemy, name: 'Battles Won' },
	{ id: EStatisticType.BattlesLost, entityType: EEntityType.Enemy, name: 'Battles Lost' },
	{ id: EStatisticType.PlayerDeaths, entityType: EEntityType.None, name: 'Player Deaths' },
	{ id: EStatisticType.TotalBattleTime, entityType: EEntityType.None, name: 'Total Battle Time' },
	{ id: EStatisticType.FastestVictory, entityType: EEntityType.Enemy, name: 'Fastest Victory' },
	{ id: EStatisticType.SkillsUsed, entityType: EEntityType.Skill, name: 'Skills Used' }
];
