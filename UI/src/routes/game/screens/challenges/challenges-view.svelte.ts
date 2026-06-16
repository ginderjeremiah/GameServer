import {
	EChallengeGoalComparison,
	EChallengeType,
	EEntityType,
	type ERarity,
	type IChallenge,
	type IItem,
	type IItemMod,
	type IPlayerChallenge,
	type ISkill
} from '$lib/api';
import { BattleAttributes, type Item } from '$lib/battle';
import {
	challengeTypeColor,
	challengeTypeName,
	rarityGlow,
	rarityLevel,
	resolveUnlockReward,
	zonesUnlockedBy
} from '$lib/common';
import { staticData } from '$stores';
import { challengeTypeUnit } from './challenge-meta';

/* ─── Types ──────────────────────────────────────────────────────────── */
export type ChallengeState = 'locked' | 'active' | 'done';
export type SortKey = 'progress' | 'rarity' | 'name';

/** A challenge's reward, resolved against the item/mod/skill reference pools. While
 *  the challenge is incomplete the reward is *sealed* (`revealed: false`) — the
 *  rarity tier and category are teased, but the name/stats stay hidden. */
export interface ResolvedReward {
	kind: 'item' | 'mod' | 'skill';
	revealed: boolean;
	/** Rarity tier (drives the rarity sort). Skills have no tier, so they resolve to `Common`. */
	rarity: ERarity;
	/** Themeable accent: the rarity hue for items/mods, the neutral skill accent for skills. */
	accent: string;
	/** Themeable rarity glow intensity token (`var(--rarity-*-glow)`), or `null` for the rarity-less
	 *  skill (which gets only the flat base glow, no tier scaling). */
	glow: string | null;
	name: string;
	/** Teaser sub-line, e.g. `Rare · Helm`, or `Skill`. */
	sub: string;
	/** Item rewards: a preview battle item for the (re-used) item tooltip. */
	item?: Item;
	/** Mod rewards: the raw mod data for the mod tooltip. */
	mod?: IItemMod;
	/** Skill rewards: the raw skill data for the skill tooltip. */
	skill?: ISkill;
}

export interface ProgressInfo {
	/** Minimisation goal (lower is better, e.g. TimeTrial) vs accumulating goal. */
	atMost: boolean;
	/** 0–100 fill for the proximity/progress bar. */
	percent: number;
	/* accumulating (atLeast) */
	value: number;
	goal: number;
	/* minimisation (atMost) */
	best: number;
	target: number;
	/** atMost only: whether a qualifying value has been recorded yet. */
	hasData: boolean;
}

export interface ChallengeVM {
	id: number;
	name: string;
	description: string;
	typeId: EChallengeType;
	goal: number;
	progress: number;
	completed: boolean;
	completedAt?: string;
	state: ChallengeState;
	prog: ProgressInfo;
	unit: string;
	/** Themeable challenge-type hue (`var(--challenge-*)`). */
	typeAccent: string;
	/** Target entity name for scoped challenges ("Goblin", "Frostspire"), else null. */
	target: string | null;
	reward: ResolvedReward | null;
	/** Names of zones this challenge unlocks (gated on it via `unlockChallengeId`), in authored order. */
	unlocksZones: string[];
}

export interface TypeGroup {
	typeId: EChallengeType;
	label: string;
	accent: string;
	items: ChallengeVM[];
}

/* ─── Reference lookups ──────────────────────────────────────────────── */
/** Goal-comparison direction is intrinsic to the type; sourced from reference data. Bulk callers pass a
 *  precomputed `byType` lookup so a rebuild doesn't re-scan the challenge-type list once per challenge. */
function comparisonFor(
	typeId: EChallengeType,
	byType?: ReadonlyMap<EChallengeType, EChallengeGoalComparison>
): EChallengeGoalComparison {
	if (byType) {
		return byType.get(typeId) ?? EChallengeGoalComparison.AtLeast;
	}
	return staticData.challengeTypes?.find((t) => t.id === typeId)?.goalComparison ?? EChallengeGoalComparison.AtLeast;
}

/** Resolve a scoped challenge's target entity name from the relevant reference pool. */
function targetName(ch: IChallenge): string | null {
	if (ch.targetEntityId == null) {
		return null;
	}
	switch (ch.entityType) {
		case EEntityType.Enemy:
			return staticData.enemies?.[ch.targetEntityId]?.name ?? null;
		case EEntityType.Zone:
			return staticData.zones?.[ch.targetEntityId]?.name ?? null;
		case EEntityType.Skill:
			return staticData.skills?.[ch.targetEntityId]?.name ?? null;
		default:
			return null;
	}
}

/** A non-owned preview item (base stats, empty mod slots) for the item tooltip. */
function buildPreviewItem(itemData: IItem): Item {
	return {
		...itemData,
		itemId: itemData.id,
		equipped: false,
		favorite: false,
		appliedMods: [],
		totalAttributes: new BattleAttributes(itemData.attributes, false)
	};
}

/* ─── Reward resolution (+ reveal gating) ────────────────────────────── */
/** Build the rich, reveal-gated reward view on top of the shared `resolveUnlockReward` resolution:
 *  the precedence, accent, rarity and sub-label come from the shared helper, and this layer adds the
 *  reveal flag, the rarity glow, and the per-kind tooltip preview (battle item / raw mod / raw skill). */
export function resolveReward(ch: IChallenge, revealed: boolean): ResolvedReward | null {
	const base = resolveUnlockReward(ch, {
		items: staticData.items,
		itemMods: staticData.itemMods,
		skills: staticData.skills
	});
	if (base == null) {
		return null;
	}
	const resolved: ResolvedReward = {
		kind: base.kind,
		revealed,
		rarity: base.rarity,
		accent: base.accent,
		// Skills carry no rarity tier, so they get no glow; items/mods glow by tier.
		glow: base.kind === 'skill' ? null : rarityGlow(base.rarity),
		name: base.name,
		sub: base.sub
	};
	switch (base.kind) {
		case 'item':
			resolved.item = buildPreviewItem(base.item);
			break;
		case 'mod':
			resolved.mod = base.mod;
			break;
		case 'skill':
			resolved.skill = base.skill;
			break;
	}
	return resolved;
}

/* ─── Progress maths (branches on comparison direction) ──────────────── */
export function progressInfo(ch: IChallenge, comparison: EChallengeGoalComparison, progress: number): ProgressInfo {
	if (comparison === EChallengeGoalComparison.AtMost) {
		// Stored progress is the player's current best (0 = no qualifying value yet).
		// Closeness to the target drives the bar; a 0→goal fill would be misleading.
		const best = progress;
		const hasData = best > 0;
		const percent = hasData ? Math.min(100, (ch.progressGoal / best) * 100) : 0;
		return { atMost: true, percent, value: 0, goal: ch.progressGoal, best, target: ch.progressGoal, hasData };
	}
	const percent = Math.min(100, (progress / Math.max(1, ch.progressGoal)) * 100);
	return { atMost: false, percent, value: progress, goal: ch.progressGoal, best: 0, target: 0, hasData: true };
}

/* ─── View-model assembly ────────────────────────────────────────────── */
export function buildChallengeVM(
	ch: IChallenge,
	player?: IPlayerChallenge,
	comparisonByType?: ReadonlyMap<EChallengeType, EChallengeGoalComparison>
): ChallengeVM {
	const completed = player?.completed ?? false;
	const progress = player?.progress ?? 0;
	const comparison = comparisonFor(ch.challengeTypeId, comparisonByType);
	const prog = progressInfo(ch, comparison, progress);
	const state: ChallengeState = completed ? 'done' : prog.percent > 0 ? 'active' : 'locked';
	return {
		id: ch.id,
		name: ch.name,
		description: ch.description,
		typeId: ch.challengeTypeId,
		goal: ch.progressGoal,
		progress,
		completed,
		completedAt: player?.completedAt,
		state,
		prog,
		unit: challengeTypeUnit(ch.challengeTypeId),
		typeAccent: challengeTypeColor(ch.challengeTypeId),
		target: targetName(ch),
		// A reward is only revealed (and inspectable) once its challenge completes.
		reward: resolveReward(ch, completed),
		unlocksZones: zonesUnlockedBy(ch.id, staticData.zones ?? []).map((z) => z.name)
	};
}

/* ─── Grouping / stats / sorting ─────────────────────────────────────── */
const CHALLENGE_TYPE_IDS = Object.values(EChallengeType).filter((v): v is EChallengeType => typeof v === 'number');

export function groupByType(list: ChallengeVM[]): TypeGroup[] {
	return CHALLENGE_TYPE_IDS.map((typeId) => ({
		typeId,
		label: challengeTypeName(typeId, staticData.challengeTypes),
		accent: challengeTypeColor(typeId),
		items: list.filter((c) => c.typeId === typeId)
	})).filter((g) => g.items.length > 0);
}

export function typeStats(items: ChallengeVM[]): { total: number; done: number } {
	return { total: items.length, done: items.filter((c) => c.state === 'done').length };
}

const STATE_RANK: Record<ChallengeState, number> = { active: 0, locked: 1, done: 2 };

export function sortChallenges(items: ChallengeVM[], sort: SortKey): ChallengeVM[] {
	const arr = items.slice();
	if (sort === 'rarity') {
		return arr.sort(
			(a, b) =>
				(b.reward ? rarityLevel(b.reward.rarity) : 0) - (a.reward ? rarityLevel(a.reward.rarity) : 0) || a.id - b.id
		);
	}
	if (sort === 'name') {
		return arr.sort((a, b) => a.name.localeCompare(b.name));
	}
	// progress: in-progress (closest first) → locked → done
	return arr.sort(
		(a, b) => STATE_RANK[a.state] - STATE_RANK[b.state] || b.prog.percent - a.prog.percent || a.id - b.id
	);
}

export interface OverallSummary {
	total: number;
	done: number;
	active: number;
	pct: number;
}

export function overallSummary(list: ChallengeVM[]): OverallSummary {
	const total = list.length;
	const done = list.filter((c) => c.state === 'done').length;
	const active = list.filter((c) => c.state === 'active').length;
	return { total, done, active, pct: Math.round((done / Math.max(1, total)) * 100) };
}

/** The in-progress challenge closest to completion (the "next up" spotlight). */
export function nextUp(list: ChallengeVM[]): ChallengeVM | null {
	return list.filter((c) => c.state === 'active').sort((a, b) => b.prog.percent - a.prog.percent)[0] ?? null;
}

export const SORT_OPTIONS: { key: SortKey; label: string }[] = [
	{ key: 'progress', label: 'Progress' },
	{ key: 'rarity', label: 'Rarity' },
	{ key: 'name', label: 'Name' }
];

/* ─── Reactive view-model ────────────────────────────────────────────── */
export class ChallengesView {
	/** Selected rail entry: a type id, or `'all'` for the overview. */
	selectedType = $state<EChallengeType | 'all'>('all');
	sort = $state<SortKey>('progress');
	playerChallenges = $state<IPlayerChallenge[]>([]);
	loading = $state(true);
	/** A failed `GetPlayerChallenges` load — kept distinct from a genuine
	 *  no-progress result so it isn't shown as a wall of zero-progress cards. */
	error = $state(false);

	// Rebuilt from reactive deps on each change (a lookup, not mutable held state).
	// eslint-disable-next-line svelte/prefer-svelte-reactivity
	readonly #progressById = $derived(new Map(this.playerChallenges.map((pc) => [pc.challengeId, pc])));

	readonly all = $derived.by<ChallengeVM[]>(() => {
		// Build the type→comparison lookup once per rebuild instead of scanning the list per challenge.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity -- transient lookup map, not held state
		const comparisonByType = new Map(
			(staticData.challengeTypes ?? []).map((t): [EChallengeType, EChallengeGoalComparison] => [t.id, t.goalComparison])
		);
		return (staticData.challenges ?? [])
			.filter(Boolean)
			.map((ch) => buildChallengeVM(ch, this.#progressById.get(ch.id), comparisonByType));
	});

	readonly groups = $derived.by(() => groupByType(this.all));
	readonly summary = $derived.by(() => overallSummary(this.all));
	readonly nextUp = $derived.by(() => nextUp(this.all));

	readonly selectedGroup = $derived.by(() =>
		this.selectedType === 'all' ? null : (this.groups.find((g) => g.typeId === this.selectedType) ?? null)
	);

	readonly detail = $derived.by<ChallengeVM[]>(() =>
		this.selectedGroup ? sortChallenges(this.selectedGroup.items, this.sort) : []
	);

	select(type: EChallengeType | 'all') {
		this.selectedType = type;
	}

	setSort(sort: SortKey) {
		this.sort = sort;
	}
}
