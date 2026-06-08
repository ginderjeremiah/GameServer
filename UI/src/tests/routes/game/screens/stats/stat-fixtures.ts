/* Shared fixtures for the Statistics screen tests. Not a test file (no
   .test/.spec suffix) so vitest does not collect it. */

import { EEntityType, EStatisticType, type IStatisticType } from '$lib/api';

/** The statistic-type reference data the backend exposes — mirrors
 *  `StatisticType.GetEntityType` / `GetBossOnly` + the `SpaceWords()` display names. */
export const SERVER_STAT_TYPES: IStatisticType[] = [
	{ id: EStatisticType.EnemiesKilled, entityType: EEntityType.Enemy, bossOnly: false, name: 'Enemies Killed' },
	{ id: EStatisticType.BossesDefeated, entityType: EEntityType.Enemy, bossOnly: true, name: 'Bosses Defeated' },
	{ id: EStatisticType.ZonesCleared, entityType: EEntityType.Zone, bossOnly: false, name: 'Zones Cleared' },
	{ id: EStatisticType.DamageDealt, entityType: EEntityType.Skill, bossOnly: false, name: 'Damage Dealt' },
	{
		id: EStatisticType.HighestSingleAttackDamage,
		entityType: EEntityType.Skill,
		bossOnly: false,
		name: 'Highest Single Attack Damage'
	},
	{ id: EStatisticType.DamageTaken, entityType: EEntityType.None, bossOnly: false, name: 'Damage Taken' },
	{ id: EStatisticType.DamageHealed, entityType: EEntityType.None, bossOnly: false, name: 'Damage Healed' },
	{
		id: EStatisticType.EnemiesEncountered,
		entityType: EEntityType.Enemy,
		bossOnly: false,
		name: 'Enemies Encountered'
	},
	{ id: EStatisticType.BattlesWon, entityType: EEntityType.Enemy, bossOnly: false, name: 'Battles Won' },
	{ id: EStatisticType.BattlesLost, entityType: EEntityType.Enemy, bossOnly: false, name: 'Battles Lost' },
	{ id: EStatisticType.BattlesAbandoned, entityType: EEntityType.Enemy, bossOnly: false, name: 'Battles Abandoned' },
	{ id: EStatisticType.PlayerDeaths, entityType: EEntityType.None, bossOnly: false, name: 'Player Deaths' },
	{ id: EStatisticType.TotalBattleTime, entityType: EEntityType.None, bossOnly: false, name: 'Total Battle Time' },
	{ id: EStatisticType.FastestVictory, entityType: EEntityType.Enemy, bossOnly: false, name: 'Fastest Victory' },
	{ id: EStatisticType.SkillsUsed, entityType: EEntityType.Skill, bossOnly: false, name: 'Skills Used' }
];
