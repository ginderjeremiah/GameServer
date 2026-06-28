import { describe, it, expect, beforeEach, vi } from 'vitest';

/*
 * Drives the real ProgressionStore against a fake in-memory backend (the same socket-read /
 * HTTP-write split the live admin uses) so the cross-catalogue save orchestration — resolving a
 * brand-new path id before the proficiencies that FK to it, then running each child saver — is
 * exercised end to end rather than mocked into a tautology.
 */

const { postMock, fetchMock, staticDataMock, toastErrorMock, referenceMock } = vi.hoisted(() => ({
	postMock: vi.fn(),
	fetchMock: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticDataMock: {} as any,
	toastErrorMock: vi.fn(),
	referenceMock: {
		skillOptions: () => [
			{ value: 0, text: 's0' },
			{ value: 1, text: 's1' },
			{ value: 2, text: 's2' }
		],
		attributeOptions: () => [
			{ value: 0, text: 'a0' },
			{ value: 1, text: 'a1' }
		]
	}
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: vi.fn(), post: postMock }, fetchSocketData: fetchMock };
});
vi.mock('$stores', () => ({ staticData: staticDataMock, toastError: toastErrorMock }));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: referenceMock }));

import { EChangeType } from '$lib/api';
import { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Rec = any;
const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v));

let serverPaths: Rec[];
let serverProfs: Rec[];

const nextId = (set: Rec[]) => (set.length ? Math.max(...set.map((r) => r.id)) + 1 : 0);

const applyPost = (endpoint: string, body: Rec) => {
	if (endpoint === 'AdminTools/AddEditPaths') {
		let id = nextId(serverPaths);
		for (const change of body as Rec[]) {
			if (change.changeType === EChangeType.Add) {
				serverPaths.push({ ...change.item, id: id++, retiredAt: change.item.retiredAt ?? null, contributions: [] });
			} else if (change.changeType === EChangeType.Edit) {
				const existing = serverPaths.find((p) => p.id === change.item.id);
				if (existing) {
					Object.assign(existing, {
						...change.item,
						retiredAt: change.item.retiredAt ?? null,
						contributions: existing.contributions
					});
				}
			}
		}
	} else if (endpoint === 'AdminTools/AddEditProficiencies') {
		let id = nextId(serverProfs);
		for (const change of body as Rec[]) {
			if (change.changeType === EChangeType.Add) {
				serverProfs.push({
					...change.item,
					id: id++,
					retiredAt: change.item.retiredAt ?? null,
					levelModifiers: [],
					levelRewards: [],
					prerequisiteIds: []
				});
			} else if (change.changeType === EChangeType.Edit) {
				const existing = serverProfs.find((p) => p.id === change.item.id);
				if (existing) {
					Object.assign(existing, {
						...change.item,
						retiredAt: change.item.retiredAt ?? null,
						levelModifiers: existing.levelModifiers,
						levelRewards: existing.levelRewards,
						prerequisiteIds: existing.prerequisiteIds
					});
				}
			}
		}
	} else if (endpoint === 'AdminTools/SetPathContributions') {
		const path = serverPaths.find((p) => p.id === body.id);
		if (path) {
			path.contributions = body.contributions;
		}
	} else if (endpoint === 'AdminTools/SetProficiencyModifiers') {
		const prof = serverProfs.find((p) => p.id === body.id);
		if (prof) {
			prof.levelModifiers = body.modifiers;
		}
	} else if (endpoint === 'AdminTools/SetProficiencyRewards') {
		const prof = serverProfs.find((p) => p.id === body.id);
		if (prof) {
			prof.levelRewards = body.rewards;
		}
	} else if (endpoint === 'AdminTools/SetProficiencyPrerequisites') {
		const prof = serverProfs.find((p) => p.id === body.id);
		if (prof) {
			prof.prerequisiteIds = body.prerequisiteIds;
		}
	}
};

const callsTo = (endpoint: string): Rec[] => postMock.mock.calls.filter((c) => c[0] === endpoint).map((c) => c[1]);

const fullTier = (over: Rec): Rec => ({
	name: 'Tier',
	description: 'd',
	iconPath: 'i.png',
	word: 'w',
	pronunciation: 'p',
	translation: 't',
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...over
});

beforeEach(() => {
	postMock.mockReset();
	fetchMock.mockReset();
	toastErrorMock.mockReset();
	serverPaths = [];
	serverProfs = [];
	fetchMock.mockImplementation(async (cmd: string) => (cmd === 'GetPaths' ? clone(serverPaths) : clone(serverProfs)));
	postMock.mockImplementation(async (endpoint: string, body: Rec) => {
		applyPost(endpoint, body);
		return {};
	});
});

const seedServer = () => {
	serverPaths = [
		{ id: 0, name: 'Fire', description: 'fire path', falloffBase: 0.6, retiredAt: null, contributions: [] }
	];
	serverProfs = [fullTier({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire T0' })];
};

describe('load & normalization', () => {
	it('seeds both catalogues, selects the first path, and shows no phantom changes', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		expect(store.loaded).toBe(true);
		expect(store.paths).toHaveLength(1);
		expect(store.selectedPathId).toBe(0);
		expect(store.totalChanges).toBe(0);
	});
});

describe('local editing', () => {
	it('patching a path marks one modified change', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.patchPath(0, (d) => (d.name = 'Inferno'));
		expect(store.counts.modified).toBe(1);
		expect(store.totalChanges).toBe(1);
	});

	it('addPath adds a path plus its first tier', async () => {
		const store = new ProgressionStore();
		await store.load();

		store.addPath();
		expect(store.counts.added).toBe(2);
		expect(store.currentTiers).toHaveLength(1);
		expect(store.currentTiers[0].pathOrdinal).toBe(0);
	});

	it('addTier appends at the next ordinal; reorderTiers renumbers contiguously', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		const second = store.addTier(0);
		expect(store.currentTiers.map((t) => t.pathOrdinal)).toEqual([0, 1]);

		store.reorderTiers(0, 0, 1);
		expect(store.currentTiers.map((t) => t.id)).toEqual([second, 0]);
		expect(store.currentTiers.map((t) => t.pathOrdinal)).toEqual([0, 1]);
	});

	it('retire is an edit; reinstate returns to clean', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.retirePath(0, true);
		expect(store.pathStatus(store.paths[0])).toBe('modified');
		expect(store.isRetired(store.paths[0])).toBe(true);

		store.retirePath(0, false);
		expect(store.pathStatus(store.paths[0])).toBe('clean');
	});

	it('removing an unsaved path drops it and its tiers and reconciles the selection', async () => {
		const store = new ProgressionStore();
		await store.load();
		store.addPath();
		const newId = store.selectedPathId as number;

		store.removePath(newId);
		expect(store.paths).toHaveLength(0);
		expect(store.profs).toHaveLength(0);
		expect(store.selectedPathId).toBeNull();
	});
});

describe('save orchestration', () => {
	it('creates a path then its tier with the resolved path id, then child collections', async () => {
		const store = new ProgressionStore();
		await store.load();

		store.addPath();
		const pathLocal = store.selectedPathId as number;
		store.patchPath(pathLocal, (d) => {
			d.name = 'Fire';
			d.falloffBase = 0.6;
		});
		const tierLocal = store.currentTiers[0].id;
		store.patchProf(tierLocal, (d) => {
			d.name = 'Fire T0';
			d.word = 'fyr';
			d.pronunciation = 'fyoor';
			d.translation = 'Fire';
			d.iconPath = 'fire.png';
		});
		store.addContribution(pathLocal);
		store.addModifier(tierLocal, 2);

		await store.save();

		// Path created first, proficiency FK remapped to the persisted path id (0).
		expect(callsTo('AdminTools/AddEditPaths')).toHaveLength(1);
		const profAdds = callsTo('AdminTools/AddEditProficiencies')[0];
		expect(profAdds[0].item.pathId).toBe(0);

		// Contributions land on the resolved path id; modifiers on the resolved proficiency id.
		expect(callsTo('AdminTools/SetPathContributions')[0]).toEqual({
			id: 0,
			contributions: [{ skillId: 0, homeTier: 0, weight: 1 }]
		});
		const mods = callsTo('AdminTools/SetProficiencyModifiers')[0];
		expect(mods.id).toBe(0);
		expect(mods.modifiers).toHaveLength(1);
		expect(mods.modifiers[0].level).toBe(2);

		// Final server truth + re-seeded, selection followed the remap, no residual changes.
		expect(serverPaths[0].contributions).toHaveLength(1);
		expect(serverProfs[0].levelModifiers).toHaveLength(1);
		expect(store.selectedPathId).toBe(0);
		expect(store.totalChanges).toBe(0);
	});

	it('sends only an identity Edit when just the path identity changed (no contributions call)', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.patchPath(0, (d) => (d.name = 'Inferno'));
		await store.save();

		const pathEdits = callsTo('AdminTools/AddEditPaths')[0];
		expect(pathEdits).toHaveLength(1);
		expect(pathEdits[0].changeType).toBe(EChangeType.Edit);
		expect(callsTo('AdminTools/SetPathContributions')).toHaveLength(0);
		expect(serverPaths[0].name).toBe('Inferno');
	});

	it('a child-only proficiency change skips the identity Edit but runs the child saver', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.addModifier(0, 3);
		await store.save();

		expect(callsTo('AdminTools/AddEditProficiencies')).toHaveLength(0);
		expect(callsTo('AdminTools/AddEditPaths')).toHaveLength(0);
		const mods = callsTo('AdminTools/SetProficiencyModifiers')[0];
		expect(mods.id).toBe(0);
		expect(mods.modifiers[0].level).toBe(3);
		expect(serverProfs[0].levelModifiers).toHaveLength(1);
	});

	it('preserves edits and surfaces an error when the first write fails (pre-commit)', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.patchPath(0, (d) => (d.name = 'Inferno'));
		postMock.mockImplementationOnce(async () => {
			throw new Error('boom');
		});

		await store.save();
		expect(toastErrorMock).toHaveBeenCalled();
		// Pre-commit failure: the working edit is preserved for a clean retry.
		expect(store.paths[0].name).toBe('Inferno');
	});
});

const reqPath = (store: ProgressionStore, id: number) => {
	const path = store.paths.find((p) => p.id === id);
	if (!path) {
		throw new Error(`no path ${id}`);
	}
	return path;
};
const reqProf = (store: ProgressionStore, id: number) => {
	const prof = store.profs.find((p) => p.id === id);
	if (!prof) {
		throw new Error(`no prof ${id}`);
	}
	return prof;
};

describe('navigation & detail mutations', () => {
	const richServer = () => {
		serverPaths = [
			{
				id: 0,
				name: 'Fire',
				description: 'd',
				falloffBase: 0.6,
				retiredAt: null,
				contributions: [{ skillId: 1, homeTier: 0, weight: 1 }]
			}
		];
		serverProfs = [
			fullTier({
				id: 0,
				pathId: 0,
				pathOrdinal: 0,
				name: 'Fire T0',
				levelModifiers: [{ level: 2, attributeId: 0, modifierTypeId: 0, amount: 5 }],
				levelRewards: [{ level: 5, rewardSkillId: 9 }]
			}),
			fullTier({ id: 1, pathId: 0, pathOrdinal: 1, name: 'Fire T1' })
		];
	};

	it('navigates selection, drill, tabs and level', async () => {
		richServer();
		const store = new ProgressionStore();
		await store.load();

		store.setPathTab('contrib');
		expect(store.pathTab).toBe('contrib');

		store.drillTier(0);
		expect(store.drilledTier?.id).toBe(0);
		expect(store.tierTab).toBe('milestones');
		expect(store.selectedLevel).toBe(5); // first reward level

		store.setTierTab('xp');
		expect(store.tierTab).toBe('xp');
		store.selectLevel(3);
		expect(store.selectedLevel).toBe(3);

		store.back();
		expect(store.drilledTier).toBeUndefined();

		store.selectPath(0);
		expect(store.selectedPathId).toBe(0);
		expect(store.pathTab).toBe('tiers');
	});

	it('edits and removes contributions', async () => {
		richServer();
		const store = new ProgressionStore();
		await store.load();

		store.updateContribution(0, 0, { weight: 0.5 });
		expect(reqPath(store, 0).contributions[0].weight).toBe(0.5);
		store.addContribution(0);
		expect(reqPath(store, 0).contributions).toHaveLength(2);
		store.removeContribution(0, 1);
		expect(reqPath(store, 0).contributions).toHaveLength(1);
	});

	it('edits milestone modifiers, rewards and payouts', async () => {
		richServer();
		const store = new ProgressionStore();
		await store.load();

		store.updateModifier(0, 0, { amount: 12 });
		expect(reqProf(store, 0).levelModifiers[0].amount).toBe(12);
		store.addModifier(0, 2);
		expect(reqProf(store, 0).levelModifiers.filter((m) => m.level === 2)).toHaveLength(2);
		store.removeModifier(0, 0);
		expect(reqProf(store, 0).levelModifiers.filter((m) => m.level === 2)).toHaveLength(1);

		store.setReward(0, 5, 7);
		expect(reqProf(store, 0).levelRewards.find((r) => r.level === 5)?.rewardSkillId).toBe(7);
		store.setReward(0, 5, -1); // clear
		expect(reqProf(store, 0).levelRewards.find((r) => r.level === 5)).toBeUndefined();

		store.addPayout(0, 8);
		expect(reqProf(store, 0).levelModifiers.some((m) => m.level === 8)).toBe(true);
		store.removePayout(0, 2);
		expect(reqProf(store, 0).levelModifiers.some((m) => m.level === 2)).toBe(false);
	});

	it('edits gateways: cross-path prerequisites (deduped)', async () => {
		richServer();
		const store = new ProgressionStore();
		await store.load();

		store.addPrerequisite(0, 1);
		store.addPrerequisite(0, 1);
		expect(reqProf(store, 0).prerequisiteIds).toEqual([1]);
		store.removePrerequisite(0, 1);
		expect(reqProf(store, 0).prerequisiteIds).toEqual([]);
	});

	it('retires/reinstates a tier, removes an unsaved tier, resets, discards and disposes', async () => {
		richServer();
		const store = new ProgressionStore();
		await store.load();

		store.retireProf(0, true);
		expect(store.isRetired(reqProf(store, 0))).toBe(true);
		store.retireProf(0, false);
		expect(store.profStatus(reqProf(store, 0))).toBe('clean');

		const newTier = store.addTier(0);
		store.drillTier(newTier);
		store.removeTier(newTier);
		expect(store.profs.some((p) => p.id === newTier)).toBe(false);
		expect(store.drilledTier).toBeUndefined();

		store.patchPath(0, (d) => (d.name = 'Changed'));
		store.resetPath(0);
		expect(store.pathStatus(reqPath(store, 0))).toBe('clean');
		store.patchProf(0, (d) => (d.name = 'Changed'));
		store.resetProf(0);
		expect(store.profStatus(reqProf(store, 0))).toBe('clean');

		expect(store.pathBaseline(0)?.name).toBe('Fire');
		expect(store.profBaseline(0)?.name).toBe('Fire T0');

		store.patchPath(0, (d) => (d.name = 'X'));
		store.discard();
		expect(store.totalChanges).toBe(0);

		store.dispose();
	});
});
