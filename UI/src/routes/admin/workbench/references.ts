import {
	EEntityType,
	type IChallenge,
	type IClass,
	type IEnemy,
	type IItem,
	type IProficiency,
	type ISkill,
	type ISkillRecipe,
	type IZone
} from '$lib/api';

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
	items: IItem[];
	classes: IClass[];
	skillRecipes: ISkillRecipe[];
	proficiencies: IProficiency[];
	skills: ISkill[];
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
	| 'enemySkill'
	| 'grantedSkillOf'
	| 'starterSkillOf'
	| 'starterEquipmentOf'
	| 'recipeResult'
	| 'recipeInput'
	| 'milestoneReward'
	| 'gearGate'
	| 'recipeCondition'
	| 'prerequisiteOf';

/** Zero-based-id lookup honouring the retirement Id-as-index invariant (a retired record keeps its slot). */
const nameByIndex = (records: { name: string }[], id: number): string => records[id]?.name ?? `#${id}`;

/** A skill-synthesis recipe is nameless — identify it in a reference list by the result skill it produces. */
const recipeName = (recipe: ISkillRecipe, skills: ISkill[]): string => nameByIndex(skills, recipe.resultSkillId);

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

const skillReferences = (
	id: number,
	{ enemies, challenges, items, classes, skillRecipes, proficiencies, skills }: ReferenceSources
): ReferenceGroup[] => {
	const inPool = enemies.filter((e) => e.skillPool.includes(id)).map((e) => e.name);
	const targetOf = challenges
		.filter((c) => c.entityType === EEntityType.Skill && c.targetEntityId === id)
		.map((c) => c.name);
	const grantedSkillOf = items.filter((i) => i.grantedSkillId === id).map((i) => i.name);
	const starterSkillOf = classes.filter((c) => c.starterSkillIds.includes(id)).map((c) => c.name);
	const recipeResultOf = skillRecipes.filter((r) => r.resultSkillId === id).map((r) => recipeName(r, skills));
	const recipeInputOf = skillRecipes.filter((r) => r.inputSkillIds.includes(id)).map((r) => recipeName(r, skills));
	const milestoneRewardOf = proficiencies
		.filter((p) => p.levelRewards.some((reward) => reward.rewardSkillId === id))
		.map((p) => p.name);
	return [
		...group('enemySkill', inPool),
		...group('challengeTarget', targetOf),
		...group('grantedSkillOf', grantedSkillOf),
		...group('starterSkillOf', starterSkillOf),
		...group('recipeResult', recipeResultOf),
		...group('recipeInput', recipeInputOf),
		...group('milestoneReward', milestoneRewardOf)
	];
};

const itemReferences = (id: number, { challenges, classes }: ReferenceSources): ReferenceGroup[] => {
	const rewardOf = challenges.filter((c) => c.rewardItemId === id).map((c) => c.name);
	const starterEquipmentOf = classes.filter((c) => c.starterEquipment.some((e) => e.itemId === id)).map((c) => c.name);
	return [...group('challengeReward', rewardOf), ...group('starterEquipmentOf', starterEquipmentOf)];
};

const itemModReferences = (id: number, { challenges }: ReferenceSources): ReferenceGroup[] =>
	group(
		'challengeReward',
		challenges.filter((c) => c.rewardItemModId === id).map((c) => c.name)
	);

const proficiencyReferences = (
	id: number,
	{ items, skillRecipes, proficiencies, skills }: ReferenceSources
): ReferenceGroup[] => {
	const gearGate = items.filter((i) => i.requiredProficiencyId === id).map((i) => i.name);
	const recipeCondition = skillRecipes
		.filter((r) => r.conditions.some((c) => c.proficiencyId === id))
		.map((r) => recipeName(r, skills));
	const prerequisiteOf = proficiencies.filter((p) => p.prerequisiteIds.includes(id)).map((p) => p.name);
	return [
		...group('gearGate', gearGate),
		...group('recipeCondition', recipeCondition),
		...group('prerequisiteOf', prerequisiteOf)
	];
};

/**
 * A path is referenced through its tiers: another (still-live) proficiency naming one of this
 * path's tiers as a cross-path prerequisite. Retiring the path freezes those tiers, so such a
 * gateway could then never open — the same soft-lock `AdminPaths.FindRetiredPathGatingLiveGateway`
 * rejects server-side. Marked `strong` since this is a real (not just advisory) save-time block.
 */
const pathReferences = (id: number, { proficiencies }: ReferenceSources): ReferenceGroup[] => {
	const tierIds = new Set(proficiencies.filter((p) => p.pathId === id).map((p) => p.id));
	const prerequisiteOf = proficiencies
		.filter((p) => p.pathId !== id && p.prerequisiteIds.some((prereqId) => tierIds.has(prereqId)))
		.map((p) => p.name);
	return group('prerequisiteOf', prerequisiteOf, true);
};

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
			return itemReferences(id, sources);
		case 'itemMods':
			return itemModReferences(id, sources);
		case 'skills':
			return skillReferences(id, sources);
		case 'proficiencies':
			return proficiencyReferences(id, sources);
		case 'paths':
			return pathReferences(id, sources);
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
		case 'grantedSkillOf':
			return `the skill granted by ${count(n, 'item')} (${names})`;
		case 'starterSkillOf':
			return `a starter skill of ${count(n, 'class', 'classes')} (${names})`;
		case 'starterEquipmentOf':
			return `starter equipment for ${count(n, 'class', 'classes')} (${names})`;
		case 'recipeResult':
			return `the result of ${count(n, 'synthesis recipe')} (${names})`;
		case 'recipeInput':
			return `an input of ${count(n, 'synthesis recipe')} (${names})`;
		case 'milestoneReward':
			return `a milestone reward of ${count(n, 'proficiency', 'proficiencies')} (${names})`;
		case 'gearGate':
			return `the required proficiency for ${count(n, 'item')} (${names})`;
		case 'recipeCondition':
			return `a condition of ${count(n, 'synthesis recipe')} (${names})`;
		case 'prerequisiteOf':
			// A path's tier is the prerequisite, not the path itself — phrase that case distinctly
			// rather than implying the path is directly gating the referencing proficiency.
			return entityKey === 'paths'
				? `home to a tier that gates ${count(n, 'proficiency', 'proficiencies')} (${names})`
				: `a prerequisite for ${count(n, 'proficiency', 'proficiencies')} (${names})`;
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
