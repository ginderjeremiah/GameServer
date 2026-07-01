import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EActivityKey, EDamageType } from '$lib/api';
import type { IPath, IPlayerProficiency, IProficiency } from '$lib/api';

/* The pure derivation (buildLexicon and friends) takes explicit args and needs no mocks; the
   ProficienciesView reads the live stores, so those are stubbed with mutable stand-ins the view-class
   tests populate before constructing the view. The real $lib/api types stay intact (buildLexicon only
   uses them as erased types). */
const { mockStaticData, mockPlayerProficiencies, mockPlayerManager, mockInventoryManager, toastError } = vi.hoisted(
	() => ({
		mockStaticData: {
			proficiencies: undefined as IProficiency[] | undefined,
			paths: undefined as IPath[] | undefined,
			skills: undefined as Record<number, { damagePortions: { type: EDamageType; weight: number }[] }> | undefined
		},
		mockPlayerProficiencies: { all: [] as IPlayerProficiency[], error: false, load: vi.fn() },
		mockPlayerManager: { selectedSkills: [] as number[] },
		mockInventoryManager: { grantedSkillIds: [] as number[] },
		toastError: vi.fn()
	})
);

vi.mock('$stores', () => ({
	staticData: mockStaticData,
	playerProficiencies: mockPlayerProficiencies,
	toastError
}));
vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));

import {
	buildLexicon,
	decipherStage,
	milestoneLevels,
	representativeTier,
	xpForLevel
} from '$routes/game/screens/proficiencies/proficiencies-lexicon';
import { ProficienciesView } from '$routes/game/screens/proficiencies/proficiencies-view.svelte';

/* ── fixtures ──────────────────────────────────────────────────────────────── */

const prof = (o: Partial<IProficiency> & { id: number; pathId: number; pathOrdinal: number }): IProficiency => ({
	name: `Prof ${o.id}`,
	description: '',
	designerNotes: '',
	iconPath: `icon-${o.id}`,
	word: `w${o.id}`,
	pronunciation: `p${o.id}`,
	translation: `t${o.id}`,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1,
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...o
});

const path = (o: Partial<IPath> & { id: number }): IPath => ({
	name: `Path ${o.id}`,
	description: '',
	designerNotes: '',
	activityKey: EActivityKey.Physical,
	...o
});

const row = (proficiencyId: number, level: number, xp = 0): IPlayerProficiency => ({ proficiencyId, level, xp });

/* A multi-path scenario reused across the state-derivation tests:
     · path 0 "Pyromancy"  — Fire(maxed) → Inferno(lvl6, frontier) → Pyroclasm(hidden), fed by skill 100
     · path 1 "Lava"       — a gateway root gated on Inferno (id 1) being maxed
     · path 2 "Blade"      — Sword(maxLevel 5, maxed) → Blade(revealed at lvl 0), fed by skill 200
     · path 3 "Retired"    — a retired path, excluded entirely */
const PROFICIENCIES: IProficiency[] = [
	prof({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire' }),
	prof({ id: 1, pathId: 0, pathOrdinal: 1, name: 'Inferno' }),
	prof({ id: 2, pathId: 0, pathOrdinal: 2, name: 'Pyroclasm' }),
	prof({ id: 3, pathId: 1, pathOrdinal: 0, name: 'Lava', prerequisiteIds: [1] }),
	prof({ id: 4, pathId: 2, pathOrdinal: 0, name: 'Sword', maxLevel: 5 }),
	prof({ id: 5, pathId: 2, pathOrdinal: 1, name: 'Blade' }),
	prof({ id: 6, pathId: 3, pathOrdinal: 0, name: 'Retired tier' })
];

const PATHS: IPath[] = [
	path({ id: 0, name: 'Pyromancy', activityKey: EActivityKey.Fire }),
	path({ id: 1, name: 'Lava' }),
	path({ id: 2, name: 'Blade', activityKey: EActivityKey.Earth }),
	path({ id: 3, name: 'Retired', retiredAt: '2026-01-01T00:00:00Z' })
];

// Fire maxed, Inferno mid-journey, Sword maxed (at its lower cap of 5). Pyroclasm / Lava / Blade unopened.
const PROGRESS: IPlayerProficiency[] = [row(0, 10), row(1, 6, 40), row(4, 5)];

const build = (over?: { player?: IPlayerProficiency[]; firing?: number[] }) =>
	buildLexicon(PROFICIENCIES, PATHS, over?.player ?? PROGRESS, over?.firing ?? [EActivityKey.Fire]);

const byId = (lexicon: ReturnType<typeof build>, id: number) => lexicon.find((p) => p.id === id);
const tier = (lexicon: ReturnType<typeof build>, pathId: number, tierId: number) =>
	byId(lexicon, pathId)?.tiers.find((t) => t.id === tierId);

/* ── decipherStage ─────────────────────────────────────────────────────────── */

describe('decipherStage', () => {
	it('is undeciphered below ceil(maxLevel / 2)', () => {
		expect(decipherStage(0, 10)).toBe('undeciphered');
		expect(decipherStage(4, 10)).toBe('undeciphered');
	});

	it('is pronunciation from ceil(maxLevel / 2) up to the cap', () => {
		expect(decipherStage(5, 10)).toBe('pronunciation');
		expect(decipherStage(9, 10)).toBe('pronunciation');
	});

	it('is translated at the cap', () => {
		expect(decipherStage(10, 10)).toBe('translated');
		expect(decipherStage(11, 10)).toBe('translated'); // defensive: never under-reports
	});

	it('rounds the pronunciation threshold up for an odd cap', () => {
		// ceil(9 / 2) === 5
		expect(decipherStage(4, 9)).toBe('undeciphered');
		expect(decipherStage(5, 9)).toBe('pronunciation');
		expect(decipherStage(9, 9)).toBe('translated');
	});
});

/* ── xpForLevel ────────────────────────────────────────────────────────────── */

describe('xpForLevel', () => {
	it('is baseXp at level 0 (the cost of the first level)', () => {
		expect(xpForLevel(100, 1.5, 0)).toBe(100);
	});

	it('compounds by xpGrowth per level', () => {
		expect(xpForLevel(100, 1.5, 1)).toBe(150);
		expect(xpForLevel(100, 1.5, 2)).toBe(225);
	});

	it('rounds to the persisted 3-dp XP scale', () => {
		// 100 × 1.1^3 = 133.1 exactly; a curve that lands beyond 3 dp is rounded.
		expect(xpForLevel(100, 1.1, 3)).toBe(133.1);
		expect(xpForLevel(10, 1.337, 2)).toBe(Math.round(10 * 1.337 ** 2 * 1000) / 1000);
	});
});

/* ── milestoneLevels ───────────────────────────────────────────────────────── */

describe('milestoneLevels', () => {
	it('is the distinct, ascending levels that grant a reward', () => {
		const p = prof({
			id: 0,
			pathId: 0,
			pathOrdinal: 0,
			maxLevel: 10,
			levelRewards: [
				{ level: 10, rewardSkillId: 1 },
				{ level: 5, rewardSkillId: 2 },
				{ level: 5, rewardSkillId: 3 }
			]
		});
		expect(milestoneLevels(p)).toEqual([5, 10]);
	});

	it('clamps rewards to the 1..maxLevel range', () => {
		const p = prof({
			id: 0,
			pathId: 0,
			pathOrdinal: 0,
			maxLevel: 5,
			levelRewards: [
				{ level: 0, rewardSkillId: 1 },
				{ level: 3, rewardSkillId: 2 },
				{ level: 8, rewardSkillId: 3 }
			]
		});
		expect(milestoneLevels(p)).toEqual([3]);
	});

	it('is empty when there are no rewards', () => {
		expect(milestoneLevels(prof({ id: 0, pathId: 0, pathOrdinal: 0 }))).toEqual([]);
	});

	it('is surfaced on each tier of the built lexicon', () => {
		const profs: IProficiency[] = [
			prof({ id: 0, pathId: 0, pathOrdinal: 0, levelRewards: [{ level: 5, rewardSkillId: 9 }] })
		];
		const paths: IPath[] = [path({ id: 0 })];
		const lexicon = buildLexicon(profs, paths, [row(0, 1)], []);
		expect(lexicon[0].tiers[0].milestoneLevels).toEqual([5]);
	});
});

/* ── buildLexicon: grouping, ordering, discovery ───────────────────────────── */

describe('buildLexicon — grouping & discovery', () => {
	it('groups proficiencies by path and orders tiers by pathOrdinal', () => {
		const lexicon = build();
		expect(byId(lexicon, 0)?.tiers.map((t) => t.id)).toEqual([0, 1]);
		expect(byId(lexicon, 2)?.tiers.map((t) => t.pathOrdinal)).toEqual([0, 1]);
	});

	it('returns only discovered paths, ordered by id', () => {
		// Pyromancy + Blade are discovered; Lava is undiscovered (gateway not met) and Retired is filtered.
		expect(build().map((p) => p.id)).toEqual([0, 2]);
	});

	it('excludes a path whose root has not been discovered', () => {
		// No rows at all → no path has an opened root → empty lexicon.
		expect(buildLexicon(PROFICIENCIES, PATHS, [], [])).toEqual([]);
	});

	it('excludes retired paths and retired proficiencies', () => {
		const lexicon = buildLexicon(PROFICIENCIES, PATHS, [...PROGRESS, row(6, 1)], []);
		expect(byId(lexicon, 3)).toBeUndefined();
	});

	it('reuses the root tier word and icon for the path (no path-level word)', () => {
		const pyromancy = byId(build(), 0);
		expect(pyromancy?.word).toBe('w0');
		expect(pyromancy?.iconPath).toBe('icon-0');
	});

	it('drops a retired mid-spine tier and re-links the spine contiguously', () => {
		const profs: IProficiency[] = [
			prof({ id: 10, pathId: 9, pathOrdinal: 0 }),
			prof({ id: 11, pathId: 9, pathOrdinal: 1, retiredAt: '2026-01-01T00:00:00Z' }),
			prof({ id: 12, pathId: 9, pathOrdinal: 2 })
		];
		const paths = [path({ id: 9 })];
		// Root maxed → the next *remaining* tier (id 12) reveals despite the retired tier between them.
		const lexicon = buildLexicon(profs, paths, [row(10, 10)], []);
		expect(byId(lexicon, 9)?.tiers.map((t) => t.id)).toEqual([10, 12]);
	});
});

/* ── buildLexicon: hidden / locked tiers ───────────────────────────────────── */

describe('buildLexicon — hiding locked tiers', () => {
	it('hides the tier past the frontier (no teasers)', () => {
		// Inferno (frontier) is not maxed, so Pyroclasm stays hidden.
		expect(tier(build(), 0, 2)).toBeUndefined();
		expect(byId(build(), 0)?.tiers).toHaveLength(2);
	});

	it('reveals a deeper tier only once its predecessor is maxed', () => {
		const maxedInferno = build({ player: [row(0, 10), row(1, 10), row(4, 5)] });
		// Inferno maxed → Pyroclasm now visible.
		expect(byId(maxedInferno, 0)?.tiers.map((t) => t.id)).toEqual([0, 1, 2]);
	});
});

/* ── buildLexicon: state derivation ────────────────────────────────────────── */

describe('buildLexicon — tier state', () => {
	it('marks a capped tier maxed', () => {
		expect(tier(build(), 0, 0)?.state).toBe('maxed');
		expect(tier(build(), 2, 4)?.state).toBe('maxed'); // Sword at its lower cap of 5
	});

	it('marks the frontier training when an equipped skill feeds the path', () => {
		expect(tier(build({ firing: [EActivityKey.Fire] }), 0, 1)?.state).toBe('training');
	});

	it('marks the frontier unlocked (not training) without an equipped contributing skill', () => {
		expect(tier(build({ firing: [] }), 0, 1)?.state).toBe('unlocked');
		// Blade's path trains on Earth, which is not firing here.
		expect(tier(build({ firing: [EActivityKey.Fire] }), 2, 5)?.state).toBe('unlocked');
	});

	it('only the frontier can be training — a maxed tier stays maxed even when fed', () => {
		// Fire Earth damage (Blade's key): Blade (frontier) trains, Sword (maxed) does not.
		const lexicon = build({ firing: [EActivityKey.Earth] });
		expect(tier(lexicon, 2, 5)?.state).toBe('training');
		expect(tier(lexicon, 2, 4)?.state).toBe('maxed');
	});

	it('flags exactly the lowest un-maxed visible tier as the frontier', () => {
		const lexicon = build();
		expect(tier(lexicon, 0, 0)?.frontier).toBe(false); // Fire (maxed)
		expect(tier(lexicon, 0, 1)?.frontier).toBe(true); // Inferno
		expect(tier(lexicon, 2, 4)?.frontier).toBe(false); // Sword (maxed)
		expect(tier(lexicon, 2, 5)?.frontier).toBe(true); // Blade
	});

	it('has no frontier when a path is fully mastered', () => {
		const profs = [prof({ id: 0, pathId: 0, pathOrdinal: 0, maxLevel: 5 })];
		const lexicon = buildLexicon(profs, [path({ id: 0 })], [row(0, 5)], []);
		expect(byId(lexicon, 0)?.tiers.every((t) => !t.frontier)).toBe(true);
		expect(tier(lexicon, 0, 0)?.state).toBe('maxed');
	});
});

/* ── buildLexicon: decipher + progress ─────────────────────────────────────── */

describe('buildLexicon — decipher stage & progress', () => {
	it('derives the decipher stage from level vs maxLevel', () => {
		const lexicon = build();
		expect(tier(lexicon, 0, 0)?.decipher).toBe('translated'); // Fire 10/10
		expect(tier(lexicon, 0, 1)?.decipher).toBe('pronunciation'); // Inferno 6/10
		expect(tier(lexicon, 2, 5)?.decipher).toBe('undeciphered'); // Blade 0/10
		expect(tier(lexicon, 2, 4)?.decipher).toBe('translated'); // Sword 5/5
	});

	it('carries the residual XP and the next-level threshold', () => {
		const inferno = tier(build(), 0, 1);
		expect(inferno?.xp).toBe(40);
		expect(inferno?.xpForNext).toBe(xpForLevel(100, 1, 6));
	});

	it('reports no next-level XP for a maxed tier', () => {
		expect(tier(build(), 0, 0)?.xpForNext).toBe(0);
	});

	it('treats an unopened-but-revealed tier as level 0', () => {
		const blade = tier(build(), 2, 5);
		expect(blade?.level).toBe(0);
		expect(blade?.xp).toBe(0);
		expect(blade?.xpForNext).toBe(xpForLevel(100, 1, 0));
	});
});

/* ── buildLexicon: gateway reachability ────────────────────────────────────── */

describe('buildLexicon — gateway reachability', () => {
	it('hides a gateway root until every prerequisite is maxed', () => {
		// Base: Inferno is level 6, so the Lava gateway (prereq id 1) is unmet → Lava undiscovered.
		expect(byId(build(), 1)).toBeUndefined();
	});

	it('reveals a gateway root once its prerequisites are maxed, even without a player row', () => {
		const lexicon = build({ player: [row(0, 10), row(1, 10), row(4, 5)] });
		const lava = byId(lexicon, 1);
		expect(lava).toBeDefined();
		expect(lava?.tiers.map((t) => t.id)).toEqual([3]);
		expect(tier(lexicon, 1, 3)?.level).toBe(0);
		expect(tier(lexicon, 1, 3)?.frontier).toBe(true);
	});

	it('keeps an already-opened gateway root visible to preserve its progress', () => {
		// Lava opened (row present) even though its prerequisite (Inferno) is not yet maxed.
		const lexicon = build({ player: [...PROGRESS, row(3, 2, 10)] });
		expect(tier(lexicon, 1, 3)?.level).toBe(2);
	});
});

/* ── representativeTier ────────────────────────────────────────────────────── */

describe('representativeTier', () => {
	it('returns the frontier tier when the path has one', () => {
		const pyromancy = byId(build(), 0);
		expect(pyromancy && representativeTier(pyromancy)?.id).toBe(1);
	});

	it('returns the deepest tier when the whole spine is mastered', () => {
		const profs = [
			prof({ id: 0, pathId: 0, pathOrdinal: 0, maxLevel: 5 }),
			prof({ id: 1, pathId: 0, pathOrdinal: 1, maxLevel: 5 })
		];
		const lexicon = buildLexicon(profs, [path({ id: 0 })], [row(0, 5), row(1, 5)], []);
		const mastered = byId(lexicon, 0);
		expect(mastered && representativeTier(mastered)?.id).toBe(1);
	});
});

/* ── ProficienciesView (selection + state) ─────────────────────────────────── */

describe('ProficienciesView', () => {
	beforeEach(() => {
		mockStaticData.proficiencies = PROFICIENCIES;
		mockStaticData.paths = PATHS;
		mockStaticData.skills = {
			100: { damagePortions: [{ type: EDamageType.Fire, weight: 1 }] },
			200: { damagePortions: [{ type: EDamageType.Earth, weight: 1 }] }
		};
		mockPlayerProficiencies.all = PROGRESS;
		mockPlayerProficiencies.error = false;
		mockPlayerManager.selectedSkills = [100];
		mockInventoryManager.grantedSkillIds = [];
		toastError.mockReset();
	});

	it('starts loading with no error', () => {
		const view = new ProficienciesView();
		expect(view.loading).toBe(true);
		expect(view.error).toBe(false);
	});

	it('derives the discovered paths from the live stores', () => {
		const view = new ProficienciesView();
		expect(view.paths.map((p) => p.id)).toEqual([0, 2]);
		expect(view.isEmpty).toBe(false);
	});

	it('trains a frontier fed only by an innate item-granted skill (not just the selected loadout)', () => {
		// The battler fires the selected loadout *plus* equipment-granted skills, so a frontier fed solely
		// by a granted skill must read as training. Nothing selected; skill 100 granted by gear feeds path 0.
		mockPlayerManager.selectedSkills = [];
		mockInventoryManager.grantedSkillIds = [100];
		const view = new ProficienciesView();
		const inferno = view.paths.find((p) => p.id === 0)?.tiers.find((t) => t.id === 1);
		expect(inferno?.state).toBe('training');
	});

	it('defaults the selection to the first path and its representative tier', () => {
		const view = new ProficienciesView();
		expect(view.selectedPath?.id).toBe(0);
		expect(view.selectedTier?.id).toBe(1); // Inferno, the frontier
	});

	it('selectPath switches paths and resets to that path’s representative tier', () => {
		const view = new ProficienciesView();
		view.selectPath(2);
		expect(view.selectedPath?.id).toBe(2);
		expect(view.selectedTier?.id).toBe(5); // Blade, Blade-path frontier
	});

	it('selectTier selects the tier and keeps its owning path selected', () => {
		const view = new ProficienciesView();
		view.selectTier(4); // Sword, on the Blade path
		expect(view.selectedPath?.id).toBe(2);
		expect(view.selectedTier?.id).toBe(4);
	});

	it('reports the empty state when no path is discovered', () => {
		mockPlayerProficiencies.all = [];
		const view = new ProficienciesView();
		expect(view.isEmpty).toBe(true);
		expect(view.selectedPath).toBeUndefined();
		expect(view.selectedTier).toBeUndefined();
	});
});
