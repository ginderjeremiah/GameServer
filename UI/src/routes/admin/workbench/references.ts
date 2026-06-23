import { EEntityType, type IChallenge, type IEnemy, type IZone } from '$lib/api';

/**
 * Computes the "referenced by" surface for a retire confirm dialog, entirely from the cached
 * reference sets (the Workbench already holds every record, retired included). Retirement never
 * breaks these references — a retired record keeps its slot and resolves by id forever — so this
 * is advisory only: it lets the admin see what a retire affects and re-point a gate/boss first.
 *
 * Framework- and store-free so it can be unit-tested in isolation; the caller injects the sources.
 */

/** The cached reference sets needed to find what points at a record (last-saved state, by id). */
export interface ReferenceSources {
	enemies: IEnemy[];
	zones: IZone[];
	challenges: IChallenge[];
}

/** A category of inbound reference and the names of the records that make it. */
export interface ReferenceGroup {
	kind: ReferenceKind;
	/** Display names of the referencing records, in id order. */
	names: string[];
	/** Elevated consequence (the gate-lockout case) — phrased more prominently. */
	strong?: boolean;
}

export type ReferenceKind =
	| 'bossOf'
	| 'spawnsIn'
	| 'spawnedBy'
	| 'challengeTarget'
	| 'unlockGate'
	| 'challengeReward'
	| 'enemySkill';

/** Zero-based-id lookup honouring the retirement Id-as-index invariant (a retired record keeps its slot). */
const nameByIndex = (records: { name: string }[], id: number): string => records[id]?.name ?? `#${id}`;

const group = (kind: ReferenceKind, names: string[], strong = false): ReferenceGroup[] =>
	names.length ? [{ kind, names, ...(strong ? { strong } : {}) }] : [];

const enemyReferences = (id: number, { enemies, zones, challenges }: ReferenceSources): ReferenceGroup[] => {
	const bossOf = zones.filter((z) => z.bossEnemyId === id).map((z) => z.name);
	const spawnsIn = (enemies[id]?.spawns ?? []).map((s) => nameByIndex(zones, s.zoneId));
	const targetOf = challenges
		.filter((c) => c.entityType === EEntityType.Enemy && c.targetEntityId === id)
		.map((c) => c.name);
	return [...group('bossOf', bossOf), ...group('spawnsIn', spawnsIn), ...group('challengeTarget', targetOf)];
};

const zoneReferences = (id: number, { enemies, challenges }: ReferenceSources): ReferenceGroup[] => {
	const spawnedBy = enemies.filter((e) => e.spawns.some((s) => s.zoneId === id)).map((e) => e.name);
	const targetOf = challenges
		.filter((c) => c.entityType === EEntityType.Zone && c.targetEntityId === id)
		.map((c) => c.name);
	return [...group('spawnedBy', spawnedBy), ...group('challengeTarget', targetOf)];
};

const challengeReferences = (id: number, { zones }: ReferenceSources): ReferenceGroup[] => {
	const unlockGate = zones.filter((z) => z.unlockChallengeId === id).map((z) => z.name);
	return group('unlockGate', unlockGate, true);
};

const skillReferences = (id: number, { enemies, challenges }: ReferenceSources): ReferenceGroup[] => {
	const inPool = enemies.filter((e) => e.skillPool.includes(id)).map((e) => e.name);
	const targetOf = challenges
		.filter((c) => c.entityType === EEntityType.Skill && c.targetEntityId === id)
		.map((c) => c.name);
	return [...group('enemySkill', inPool), ...group('challengeTarget', targetOf)];
};

const rewardReferences = (
	id: number,
	{ challenges }: ReferenceSources,
	key: 'rewardItemId' | 'rewardItemModId'
): ReferenceGroup[] =>
	group(
		'challengeReward',
		challenges.filter((c) => c[key] === id).map((c) => c.name)
	);

/** Every record that references the given retireable record, grouped by relationship (empty when none). */
export function computeReferences(entityKey: string, id: number, sources: ReferenceSources): ReferenceGroup[] {
	switch (entityKey) {
		case 'enemies':
			return enemyReferences(id, sources);
		case 'zones':
			return zoneReferences(id, sources);
		case 'challenges':
			return challengeReferences(id, sources);
		case 'items':
			return rewardReferences(id, sources, 'rewardItemId');
		case 'itemMods':
			return rewardReferences(id, sources, 'rewardItemModId');
		case 'skills':
			return skillReferences(id, sources);
		default:
			return [];
	}
}

// ── Confirm-dialog formatting ──

/** Cap on names listed per group before collapsing the tail to a "+N more". */
const MAX_NAMES = 6;

const count = (n: number, singular: string, plural = `${singular}s`): string => `${n} ${n === 1 ? singular : plural}`;

const joinNames = (names: string[]): string =>
	names.length <= MAX_NAMES
		? names.join(', ')
		: `${names.slice(0, MAX_NAMES).join(', ')} +${names.length - MAX_NAMES} more`;

/** A noun-phrase completing "<name> is …" for one reference group. */
const clauseFor = (entityKey: string, ref: ReferenceGroup): string => {
	const names = joinNames(ref.names);
	const n = ref.names.length;
	switch (ref.kind) {
		case 'bossOf':
			return `the authored boss of ${names}`;
		case 'spawnsIn':
			return `in the spawn table of ${count(n, 'zone')} (${names})`;
		case 'spawnedBy':
			return `the spawn location for ${count(n, 'enemy', 'enemies')} (${names})`;
		case 'challengeTarget': {
			const base = `the target of ${count(n, 'challenge')} (${names})`;
			// Only an enemy target can become unsatisfiable once it drops out of random spawns.
			return entityKey === 'enemies'
				? `${base}, which may become unsatisfiable once it no longer spawns randomly`
				: base;
		}
		case 'unlockGate':
			return `the unlock gate for ${names} — new players will be unable to enter ${n === 1 ? 'it' : 'them'}`;
		case 'challengeReward':
			return `the reward for ${count(n, 'challenge')} (${names})`;
		case 'enemySkill':
			return `in the skill pool of ${count(n, 'enemy', 'enemies')} (${names})`;
	}
};

/** Oxford-comma join: "a" · "a and b" · "a, b, and c". */
const joinClauses = (clauses: string[]): string => {
	if (clauses.length <= 1) {
		return clauses[0] ?? '';
	}
	if (clauses.length === 2) {
		return `${clauses[0]} and ${clauses[1]}`;
	}
	return `${clauses.slice(0, -1).join(', ')}, and ${clauses[clauses.length - 1]}`;
};

/**
 * Prose body for the retire confirm dialog, enumerating the inbound references. Plain text (the
 * modal renders the body as-is), matching the existing confirm-dialog convention.
 */
export function formatReferenceBody(entityKey: string, name: string, groups: ReferenceGroup[]): string {
	const list = joinClauses(groups.map((g) => clauseFor(entityKey, g)));
	// A strong reference (the gate-lockout case) has a real consequence for new players that
	// "the id still resolves" doesn't soften, so close by pointing at the fix rather than reassuring.
	const closing = groups.some((g) => g.strong)
		? 'Existing references still resolve by id, but re-point the affected records first if that consequence is unintended.'
		: 'Retiring takes it out of circulation for new content, though existing references still resolve by id.';
	return `${name} is ${list}. ${closing}`;
}
