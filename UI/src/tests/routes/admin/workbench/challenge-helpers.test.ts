import { describe, it, expect } from 'vitest';
import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	EStatisticType,
	type IChallenge,
	type IChallengeType
} from '../../../../lib/api';
import {
	challengeSentence,
	claimedItemMap,
	claimedModMap,
	deriveFromType,
	fmtMs,
	fmtNum,
	goalComparisonOf,
	goalUnit,
	trackedKind,
	trackedLabel,
	typeBossOnly,
	typeEntityType,
	typeStatistic
} from '../../../../routes/admin/workbench/entities/challenge-helpers';

// Mirrors the GET /api/Challenges/ChallengeTypes metadata the page consumes.
const TYPES: IChallengeType[] = [
	{
		id: EChallengeType.EnemiesKilled,
		name: 'Enemies Killed',
		goalComparison: EChallengeGoalComparison.AtLeast,
		statisticType: {
			id: EStatisticType.EnemiesKilled,
			entityType: EEntityType.Enemy,
			bossOnly: false,
			name: 'Enemies Killed'
		}
	},
	{
		id: EChallengeType.BossesDefeated,
		name: 'Bosses Defeated',
		goalComparison: EChallengeGoalComparison.AtLeast,
		statisticType: {
			id: EStatisticType.BossesDefeated,
			entityType: EEntityType.Enemy,
			bossOnly: true,
			name: 'Bosses Defeated'
		}
	},
	{
		id: EChallengeType.ZonesCleared,
		name: 'Zones Cleared',
		goalComparison: EChallengeGoalComparison.AtLeast,
		statisticType: {
			id: EStatisticType.ZonesCleared,
			entityType: EEntityType.Zone,
			bossOnly: false,
			name: 'Zones Cleared'
		}
	},
	{
		id: EChallengeType.TimeTrial,
		name: 'Time Trial',
		goalComparison: EChallengeGoalComparison.AtMost,
		statisticType: {
			id: EStatisticType.FastestVictory,
			entityType: EEntityType.None,
			bossOnly: false,
			name: 'Fastest Victory'
		}
	},
	{
		id: EChallengeType.LevelReached,
		name: 'Level Reached',
		goalComparison: EChallengeGoalComparison.AtLeast,
		statisticType: undefined
	},
	{
		id: EChallengeType.DamageDealt,
		name: 'Damage Dealt',
		goalComparison: EChallengeGoalComparison.AtLeast,
		statisticType: {
			id: EStatisticType.DamageDealt,
			entityType: EEntityType.Skill,
			bossOnly: false,
			name: 'Damage Dealt'
		}
	},
	{
		id: EChallengeType.SkillsUsed,
		name: 'Skills Used',
		goalComparison: EChallengeGoalComparison.AtLeast,
		statisticType: {
			id: EStatisticType.SkillsUsed,
			entityType: EEntityType.Skill,
			bossOnly: false,
			name: 'Skills Used'
		}
	}
];

const make = (over: Partial<IChallenge>): IChallenge => {
	const type = over.challengeTypeId ?? EChallengeType.EnemiesKilled;
	const stat = typeStatistic(TYPES, type);
	return {
		id: 1,
		name: 'Test',
		description: '',
		challengeTypeId: type,
		statisticType: stat?.id,
		entityType: stat?.entityType ?? EEntityType.None,
		progressGoal: 10,
		...over
	};
};

// Tests resolve every entity dimension to a fixed token so the sentence is deterministic.
const resolve = (etype: EEntityType, id: number) => `${EEntityType[etype]}#${id}`;

describe('type-derived metadata', () => {
	it('reads the statistic + entity dimension off the type', () => {
		expect(typeStatistic(TYPES, EChallengeType.DamageDealt)?.id).toBe(EStatisticType.DamageDealt);
		expect(typeEntityType(TYPES, EChallengeType.DamageDealt)).toBe(EEntityType.Skill);
		expect(typeEntityType(TYPES, EChallengeType.BossesDefeated)).toBe(EEntityType.Enemy);
	});

	it('treats a stat-less type as having no statistic', () => {
		expect(typeStatistic(TYPES, EChallengeType.LevelReached)).toBeNull();
		expect(typeEntityType(TYPES, EChallengeType.LevelReached)).toBe(EEntityType.None);
	});

	it('exposes the goal comparison (Time Trial completes at most)', () => {
		expect(goalComparisonOf(TYPES, EChallengeType.EnemiesKilled)).toBe(EChallengeGoalComparison.AtLeast);
		expect(goalComparisonOf(TYPES, EChallengeType.TimeTrial)).toBe(EChallengeGoalComparison.AtMost);
	});

	it('flags a boss-only statistic so the editor can restrict the enemy target picker', () => {
		expect(typeBossOnly(TYPES, EChallengeType.BossesDefeated)).toBe(true);
		expect(typeBossOnly(TYPES, EChallengeType.EnemiesKilled)).toBe(false);
		// A stat-less type carries no boss-only flag.
		expect(typeBossOnly(TYPES, EChallengeType.LevelReached)).toBe(false);
	});
});

describe('trackedKind / trackedLabel', () => {
	it('reports a statistic-tracked challenge', () => {
		const c = make({ challengeTypeId: EChallengeType.EnemiesKilled });
		expect(trackedKind(c)).toBe('stat');
		expect(trackedLabel(c, TYPES)).toBe('Enemies Killed');
	});

	it('reports the player-level case for Level Reached', () => {
		const c = make({ challengeTypeId: EChallengeType.LevelReached });
		expect(trackedKind(c)).toBe('level');
		expect(trackedLabel(c, TYPES)).toBe('Player Level');
	});
});

describe('goalUnit', () => {
	it('derives the unit from the statistic, or level/goal otherwise', () => {
		expect(goalUnit(make({ challengeTypeId: EChallengeType.EnemiesKilled }))).toBe('kills');
		expect(goalUnit(make({ challengeTypeId: EChallengeType.TimeTrial }))).toBe('ms');
		expect(goalUnit(make({ challengeTypeId: EChallengeType.LevelReached }))).toBe('level');
	});
});

describe('challengeSentence', () => {
	it('handles global vs scoped enemy kills', () => {
		expect(challengeSentence(make({ progressGoal: 1000 }), resolve)).toBe('Defeat 1,000 enemies');
		expect(challengeSentence(make({ progressGoal: 50, targetEntityId: 7 }), resolve)).toBe('Defeat 50 Enemy#7');
	});

	it('pluralizes bosses and zones by goal', () => {
		expect(challengeSentence(make({ challengeTypeId: EChallengeType.BossesDefeated, progressGoal: 1 }), resolve)).toBe(
			'Defeat 1 boss'
		);
		expect(challengeSentence(make({ challengeTypeId: EChallengeType.ZonesCleared, progressGoal: 3 }), resolve)).toBe(
			'Clear 3 zones'
		);
	});

	it('scopes bosses defeated to a specific boss when targeted', () => {
		expect(
			challengeSentence(
				make({ challengeTypeId: EChallengeType.BossesDefeated, progressGoal: 10, targetEntityId: 4 }),
				resolve
			)
		).toBe('Defeat Enemy#4 10 times');
	});

	it('renders the time-trial "in under" phrasing with a formatted duration', () => {
		expect(challengeSentence(make({ challengeTypeId: EChallengeType.TimeTrial, progressGoal: 30000 }), resolve)).toBe(
			'Win a battle in under 30s'
		);
	});

	it('scopes damage dealt to a skill and reaches a level', () => {
		expect(
			challengeSentence(
				make({ challengeTypeId: EChallengeType.DamageDealt, progressGoal: 100000, targetEntityId: 2 }),
				resolve
			)
		).toBe('Deal 100,000 damage with Skill#2');
		expect(challengeSentence(make({ challengeTypeId: EChallengeType.LevelReached, progressGoal: 20 }), resolve)).toBe(
			'Reach level 20'
		);
	});
});

describe('reward exclusivity maps', () => {
	const challenges: IChallenge[] = [
		make({ id: 1, rewardItemId: 10, rewardItemModId: 100 }),
		make({ id: 2, rewardItemId: 20, rewardItemModId: undefined }),
		make({ id: 3, rewardItemId: undefined, rewardItemModId: 300 })
	];

	it('maps every claimed reward to its owning challenge, excluding the edited one', () => {
		const items = claimedItemMap(challenges, 2);
		expect(items.get(10)?.id).toBe(1);
		expect(items.has(20)).toBe(false); // challenge 2 is excluded so its own reward stays selectable
		const mods = claimedModMap(challenges, 1);
		expect(mods.get(300)?.id).toBe(3);
		expect(mods.has(100)).toBe(false);
	});
});

describe('deriveFromType', () => {
	it('re-derives statistic, entity, and clears the target when the type changes', () => {
		const c = make({ challengeTypeId: EChallengeType.EnemiesKilled, targetEntityId: 5 });
		deriveFromType(c, TYPES, EChallengeType.DamageDealt);
		expect(c.challengeTypeId).toBe(EChallengeType.DamageDealt);
		expect(c.statisticType).toBe(EStatisticType.DamageDealt);
		expect(c.entityType).toBe(EEntityType.Skill);
		expect(c.targetEntityId).toBeUndefined();
	});

	it('clears the statistic for a stat-less type', () => {
		const c = make({ challengeTypeId: EChallengeType.EnemiesKilled });
		deriveFromType(c, TYPES, EChallengeType.LevelReached);
		expect(c.statisticType).toBeUndefined();
		expect(c.entityType).toBe(EEntityType.None);
	});
});

describe('number formatting', () => {
	it('formats counts with grouping and durations in s/ms', () => {
		expect(fmtNum(100000)).toBe('100,000');
		expect(fmtMs(30000)).toBe('30s');
		expect(fmtMs(1500)).toBe('1.5s');
		expect(fmtMs(750)).toBe('750ms');
	});
});
