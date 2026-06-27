import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	EStatisticType,
	type IChallenge,
	type IChallengeType,
	type IStatisticType
} from '$lib/api';

/**
 * Pure challenge-domain derivations shared by the workbench's condition/reward
 * editors. A challenge's tracked statistic, target-entity type, and goal
 * comparison are all defined by its {@link IChallengeType} (the `/ChallengeTypes`
 * metadata) — never edited directly — so every helper reads from that metadata
 * rather than the record. Kept framework-free so they can be unit-tested in
 * isolation; callers supply the type list and an entity-name resolver.
 */

/** The challenge type whose statistic is null tracks the player's level instead. */
export const LEVEL_REACHED = EChallengeType.LevelReached;

export const challengeTypeById = (types: IChallengeType[], id: EChallengeType): IChallengeType | undefined =>
	types.find((t) => t.id === id);

/** The statistic a type is bucketed by, or null when the type has no statistic. */
export const typeStatistic = (types: IChallengeType[], id: EChallengeType): IStatisticType | null =>
	challengeTypeById(types, id)?.statisticType ?? null;

/** The entity dimension a type's statistic is scoped by (None when global or stat-less). */
export const typeEntityType = (types: IChallengeType[], id: EChallengeType): EEntityType =>
	typeStatistic(types, id)?.entityType ?? EEntityType.None;

export const goalComparisonOf = (types: IChallengeType[], id: EChallengeType): EChallengeGoalComparison =>
	challengeTypeById(types, id)?.goalComparison ?? EChallengeGoalComparison.AtLeast;

/**
 * Whether a type's tracked statistic is only ever recorded for boss enemies
 * (`IStatisticType.bossOnly`). When true, an enemy-scoped target must be a boss,
 * so the editor's target-entity picker is restricted to bosses. Sourced from the
 * backend statistic-type metadata so the rule has a single source of truth rather
 * than being special-cased per challenge type.
 */
export const typeBossOnly = (types: IChallengeType[], id: EChallengeType): boolean =>
	typeStatistic(types, id)?.bossOnly ?? false;

export const entityTypeName = (etype: EEntityType): string => EEntityType[etype] ?? 'None';

/** What a challenge tracks progress against: a statistic, the player's level, or nothing. */
export type TrackedKind = 'stat' | 'level' | 'none';
export const trackedKind = (c: IChallenge): TrackedKind =>
	c.statisticType != null ? 'stat' : c.challengeTypeId === LEVEL_REACHED ? 'level' : 'none';

export const trackedLabel = (c: IChallenge, types: IChallengeType[]): string => {
	if (c.statisticType != null) {
		return typeStatistic(types, c.challengeTypeId)?.name ?? 'Statistic';
	}
	return c.challengeTypeId === LEVEL_REACHED ? 'Player Level' : 'No statistic';
};

/** Goal-amount unit per statistic (UI-only; the comparison/goal live on the record). */
const STAT_UNIT: Record<EStatisticType, string> = {
	[EStatisticType.EnemiesKilled]: 'kills',
	[EStatisticType.BossesDefeated]: 'bosses',
	[EStatisticType.ZonesCleared]: 'zones',
	[EStatisticType.DamageDealt]: 'damage',
	[EStatisticType.HighestSingleAttackDamage]: 'damage',
	[EStatisticType.DamageTaken]: 'damage',
	[EStatisticType.DamageHealed]: 'healed',
	[EStatisticType.EnemiesEncountered]: 'encounters',
	[EStatisticType.BattlesWon]: 'wins',
	[EStatisticType.BattlesLost]: 'losses',
	[EStatisticType.BattlesAbandoned]: 'abandons',
	[EStatisticType.PlayerDeaths]: 'deaths',
	[EStatisticType.TotalBattleTime]: 'ms',
	[EStatisticType.FastestVictory]: 'ms',
	[EStatisticType.SkillsUsed]: 'skills',
	[EStatisticType.CriticalHits]: 'crits',
	[EStatisticType.CriticalDamageDealt]: 'damage',
	[EStatisticType.AttacksDodged]: 'dodges',
	[EStatisticType.DamageDodged]: 'damage',
	[EStatisticType.AttacksBlocked]: 'blocks',
	[EStatisticType.DamageBlocked]: 'damage'
};

export const goalUnit = (c: IChallenge): string => {
	if (c.statisticType != null) {
		return STAT_UNIT[c.statisticType] ?? 'goal';
	}
	return c.challengeTypeId === LEVEL_REACHED ? 'level' : 'goal';
};

export const fmtNum = (n: number): string => Number(n || 0).toLocaleString('en-US');
export const fmtMs = (ms: number): string => (ms >= 1000 ? `${(ms / 1000).toFixed(ms % 1000 ? 1 : 0)}s` : `${ms}ms`);

/** Resolves a target entity's display name for the given entity dimension. */
export type EntityNameResolver = (entityType: EEntityType, id: number) => string | null;

/**
 * Player-facing objective sentence — makes the type / target / goal combination
 * legible at a glance (mirrors the in-game challenge text).
 */
export function challengeSentence(c: IChallenge, resolveEntityName: EntityNameResolver): string {
	const g = c.progressGoal;
	const tgt = c.targetEntityId != null ? resolveEntityName(c.entityType, c.targetEntityId) : null;
	switch (c.challengeTypeId) {
		case EChallengeType.EnemiesKilled:
			return tgt ? `Defeat ${fmtNum(g)} ${tgt}` : `Defeat ${fmtNum(g)} enemies`;
		case EChallengeType.BossesDefeated:
			return tgt
				? `Defeat ${tgt} ${fmtNum(g)} ${g === 1 ? 'time' : 'times'}`
				: `Defeat ${fmtNum(g)} ${g === 1 ? 'boss' : 'bosses'}`;
		case EChallengeType.ZonesCleared:
			return tgt
				? `Clear ${tgt} ${fmtNum(g)} ${g === 1 ? 'time' : 'times'}`
				: `Clear ${fmtNum(g)} ${g === 1 ? 'zone' : 'zones'}`;
		case EChallengeType.TimeTrial:
			return tgt ? `Defeat ${tgt} in under ${fmtMs(g)}` : `Win a battle in under ${fmtMs(g)}`;
		case EChallengeType.LevelReached:
			return `Reach level ${fmtNum(g)}`;
		case EChallengeType.DamageDealt:
			return tgt ? `Deal ${fmtNum(g)} damage with ${tgt}` : `Deal ${fmtNum(g)} total damage`;
		case EChallengeType.BattlesWon:
			return tgt ? `Win ${fmtNum(g)} battles against ${tgt}` : `Win ${fmtNum(g)} battles`;
		case EChallengeType.SkillsUsed:
			return tgt ? `Use ${tgt} ${fmtNum(g)} times` : `Use skills ${fmtNum(g)} times`;
		default:
			return `${fmtNum(g)} ${goalUnit(c)}`;
	}
}

/**
 * Each item / mod may be unlocked by at most one challenge. These map an
 * already-claimed reward id to the challenge that owns it, excluding the
 * challenge being edited so its own reward stays selectable.
 */
export function claimedItemMap(challenges: IChallenge[], exceptId: number): Map<number, IChallenge> {
	const map = new Map<number, IChallenge>();
	for (const c of challenges) {
		if (c.id !== exceptId && c.rewardItemId != null) {
			map.set(c.rewardItemId, c);
		}
	}
	return map;
}

export function claimedModMap(challenges: IChallenge[], exceptId: number): Map<number, IChallenge> {
	const map = new Map<number, IChallenge>();
	for (const c of challenges) {
		if (c.id !== exceptId && c.rewardItemModId != null) {
			map.set(c.rewardItemModId, c);
		}
	}
	return map;
}

/** Re-derive the statistic + entity a challenge tracks from a (possibly new) type. */
export function deriveFromType(c: IChallenge, types: IChallengeType[], typeId: EChallengeType): void {
	const stat = typeStatistic(types, typeId);
	c.challengeTypeId = typeId;
	c.statisticType = stat?.id;
	c.entityType = stat?.entityType ?? EEntityType.None;
	c.targetEntityId = undefined;
}
