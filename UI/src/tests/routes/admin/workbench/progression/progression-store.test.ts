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

import { EActivityKey, EChangeType } from '$lib/api';
import { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
type Rec = any;
const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v));

let serverPaths: Rec[];
let serverProfs: Rec[];

const nextId = (set: Rec[]) => (set.length ? Math.max(...set.map((r) => r.id)) + 1 : 0);

/**
 * Mirrors the backend's AdminPaths.FindRetiredPathGatingLiveGateway guard: rejects newly retiring a
 * path if, against the *current* server graph, one of its tiers still gates a live gateway. Lets
 * save-orchestration tests exercise the real ordering hazard from #1776 instead of asserting it away.
 */
const findRetiredPathGatingLiveGatewayRejection = (changes: Rec[]): string | undefined => {
	for (const change of changes) {
		if (change.changeType !== EChangeType.Edit || !change.item.retiredAt) {
			continue;
		}
		const path = serverPaths.find((p) => p.id === change.item.id);
		if (!path || path.retiredAt) {
			continue;
		}
		const retiredTierIds = new Set(serverProfs.filter((p) => p.pathId === path.id).map((p) => p.id));
		for (const gateway of serverProfs) {
			if (gateway.retiredAt || retiredTierIds.has(gateway.id)) {
				continue;
			}
			if (gateway.prerequisiteIds.some((id: number) => retiredTierIds.has(id))) {
				return `Retiring path '${path.name}' would soft-lock live proficiency '${gateway.name}'.`;
			}
		}
	}
	return undefined;
};

/**
 * Mirrors the backend's AdminProficiencies.FindShrunkenMaxLevelViolation guard: rejects an identity
 * Edit whose new MaxLevel falls below the level of a modifier/reward the proficiency *currently has
 * persisted* (i.e. as of this call, not this request's own child-collection changes).
 */
const findShrunkenMaxLevelRejection = (changes: Rec[]): string | undefined => {
	for (const change of changes) {
		if (change.changeType !== EChangeType.Edit) {
			continue;
		}
		const existing = serverProfs.find((p) => p.id === change.item.id);
		if (!existing) {
			continue;
		}
		const highest = Math.max(
			-Infinity,
			...existing.levelModifiers.map((m: Rec) => m.level),
			...existing.levelRewards.map((r: Rec) => r.level)
		);
		if (highest > change.item.maxLevel) {
			return `Proficiency '${existing.name}' has a payout at level ${highest}, above the new cap of ${change.item.maxLevel}.`;
		}
	}
	return undefined;
};

const applyPost = (endpoint: string, body: Rec) => {
	if (endpoint === 'AdminTools/AddEditPaths') {
		const rejection = findRetiredPathGatingLiveGatewayRejection(body as Rec[]);
		if (rejection) {
			throw new Error(rejection);
		}
		let id = nextId(serverPaths);
		for (const change of body as Rec[]) {
			if (change.changeType === EChangeType.Add) {
				serverPaths.push({ ...change.item, id: id++, retiredAt: change.item.retiredAt ?? null });
			} else if (change.changeType === EChangeType.Edit) {
				const existing = serverPaths.find((p) => p.id === change.item.id);
				if (existing) {
					Object.assign(existing, {
						...change.item,
						retiredAt: change.item.retiredAt ?? null
					});
				}
			}
		}
	} else if (endpoint === 'AdminTools/AddEditProficiencies') {
		const rejection = findShrunkenMaxLevelRejection(body as Rec[]);
		if (rejection) {
			throw new Error(rejection);
		}
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
		for (const change of body as Rec[]) {
			const prof = serverProfs.find((p) => p.id === change.id);
			if (prof) {
				prof.prerequisiteIds = change.prerequisiteIds;
			}
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
		{ id: 0, name: 'Fire', description: 'fire path', activityKey: EActivityKey.Physical, retiredAt: null }
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

	it('surfaces a persistent error and stays unloaded when the initial load fails', async () => {
		fetchMock.mockRejectedValue(new Error('network down'));
		const store = new ProgressionStore();
		await store.load();

		expect(store.loaded).toBe(false);
		expect(store.error).toBe('network down');
		expect(toastErrorMock).toHaveBeenCalledWith('network down');
	});

	it('clears the error and loads once a retry succeeds', async () => {
		fetchMock.mockRejectedValueOnce(new Error('network down'));
		const store = new ProgressionStore();
		await store.load();
		expect(store.error).toBe('network down');

		seedServer();
		await store.load();

		expect(store.error).toBeNull();
		expect(store.loaded).toBe(true);
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
			d.activityKey = EActivityKey.Fire;
		});
		const tierLocal = store.currentTiers[0].id;
		store.patchProf(tierLocal, (d) => {
			d.name = 'Fire T0';
			d.word = 'fyr';
			d.pronunciation = 'fyoor';
			d.translation = 'Fire';
			d.iconPath = 'fire.png';
		});
		store.addModifier(tierLocal, 2);

		await store.save();

		// Path created first, proficiency FK remapped to the persisted path id (0).
		expect(callsTo('AdminTools/AddEditPaths')).toHaveLength(1);
		const profAdds = callsTo('AdminTools/AddEditProficiencies')[0];
		expect(profAdds[0].item.pathId).toBe(0);

		// Modifiers land on the resolved proficiency id.
		const mods = callsTo('AdminTools/SetProficiencyModifiers')[0];
		expect(mods.id).toBe(0);
		expect(mods.modifiers).toHaveLength(1);
		expect(mods.modifiers[0].level).toBe(2);

		// Final server truth + re-seeded, selection followed the remap, no residual changes.
		expect(serverProfs[0].levelModifiers).toHaveLength(1);
		expect(store.selectedPathId).toBe(0);
		expect(store.totalChanges).toBe(0);
	});

	it('resolves a same-save-new-path proficiency by identity content, not position, when a concurrent add lands at a lower id (#1856)', async () => {
		const store = new ProgressionStore();
		await store.load();

		store.addPath();
		const pathLocal = store.selectedPathId as number;
		store.patchPath(pathLocal, (d) => {
			d.name = 'Fire';
			d.activityKey = EActivityKey.Fire;
		});
		const tierLocal = store.currentTiers[0].id;
		store.patchProf(tierLocal, (d) => {
			d.name = 'Fire T0';
			d.word = 'fyr';
			d.pronunciation = 'fyoor';
			d.translation = 'Fire';
			d.iconPath = 'fire.png';
		});
		store.addModifier(tierLocal, 2);

		// Simulate another admin's proficiency add landing, with a lower id than this session's own
		// about-to-be-persisted tier, in the window between this session's AddEditProficiencies POST
		// and its refetch. Since the tier's own path is *also* new in this save, its `pathId` in the
		// diff still carries the path's local (negative) id — the scenario the review on #1856 flagged.
		postMock.mockImplementation(async (endpoint: string, body: Rec) => {
			if (endpoint === 'AdminTools/AddEditProficiencies') {
				serverProfs.push(fullTier({ id: nextId(serverProfs), pathId: 0, pathOrdinal: 5, name: 'Not mine' }));
			}
			applyPost(endpoint, body);
			return {};
		});

		await store.save();

		// The foreign tier must be untouched; this session's own modifier must land on its own tier,
		// not the foreign one that happened to sort to a lower id.
		const mine = serverProfs.find((p) => p.name === 'Fire T0');
		const foreign = serverProfs.find((p) => p.name === 'Not mine');
		expect(mine?.levelModifiers).toHaveLength(1);
		expect(foreign?.levelModifiers).toHaveLength(0);
	});

	it('sends only an identity Edit when just the path identity changed', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.patchPath(0, (d) => (d.name = 'Inferno'));
		await store.save();

		const pathEdits = callsTo('AdminTools/AddEditPaths')[0];
		expect(pathEdits).toHaveLength(1);
		expect(pathEdits[0].changeType).toBe(EChangeType.Edit);
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

	it('batches cross-path prerequisite changes into one call so a gateway swap posts as a single combined set', async () => {
		serverPaths = [{ id: 0, name: 'Fire', description: 'd', activityKey: EActivityKey.Fire, retiredAt: null }];
		serverProfs = [
			fullTier({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire T0', prerequisiteIds: [1] }),
			fullTier({ id: 1, pathId: 0, pathOrdinal: 1, name: 'Fire T1' })
		];
		const store = new ProgressionStore();
		await store.load();

		// Swap the gateway: tier 0 drops its prerequisite on tier 1, tier 1 gains one on tier 0. Posted
		// per tier in submission order, the backend would see tier 0's still-live edge and tier 1's new
		// one at the same time and reject it as a cycle — batching avoids that entirely.
		store.removePrerequisite(0, 1);
		store.addPrerequisite(1, 0);

		await store.save();

		const prereqCalls = callsTo('AdminTools/SetProficiencyPrerequisites');
		expect(prereqCalls).toHaveLength(1);
		expect(prereqCalls[0]).toEqual([
			{ id: 0, prerequisiteIds: [] },
			{ id: 1, prerequisiteIds: [0] }
		]);
		expect(serverProfs.find((p) => p.id === 0)?.prerequisiteIds).toEqual([]);
		expect(serverProfs.find((p) => p.id === 1)?.prerequisiteIds).toEqual([0]);
	});

	it('a non-retiring save keeps every prerequisite change in one batch, even spanning a brand-new tier', async () => {
		serverPaths = [{ id: 0, name: 'Fire', description: 'd', activityKey: EActivityKey.Fire, retiredAt: null }];
		serverProfs = [
			fullTier({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire T0', prerequisiteIds: [1] }),
			fullTier({ id: 1, pathId: 0, pathOrdinal: 1, name: 'Fire T1' })
		];
		const store = new ProgressionStore();
		await store.load();

		// Reverse the persisted 0/1 edge (tier 0 drops its prerequisite on tier 1, tier 1 gains one on
		// tier 0) while tier 0 *also* gains a prerequisite on a brand-new tier added in this same save.
		// No path is being retired, so the early-post split from #1776 must not kick in here: splitting
		// this into two POSTs (tier 1's change resolvable now, tier 0's and the new tier's deferred for
		// id remap) would have the backend see tier 0's still-live edge on tier 1 and tier 1's new edge
		// on tier 0 at the same time and reject the whole save as a transient cycle, even though neither
		// intermediate state nor the final one actually cycles.
		const newTierId = store.addTier(0);
		store.removePrerequisite(0, 1);
		store.addPrerequisite(0, newTierId);
		store.addPrerequisite(1, 0);

		await store.save();

		const prereqCalls = callsTo('AdminTools/SetProficiencyPrerequisites');
		expect(prereqCalls).toHaveLength(1);
		expect(prereqCalls[0]).toEqual([
			{ id: 2, prerequisiteIds: [] },
			{ id: 0, prerequisiteIds: [2] },
			{ id: 1, prerequisiteIds: [0] }
		]);
		expect(serverProfs.find((p) => p.id === 0)?.prerequisiteIds).toEqual([2]);
		expect(serverProfs.find((p) => p.id === 1)?.prerequisiteIds).toEqual([0]);
	});

	it('retiring a path and removing the gateway prerequisite it now soft-locks succeeds in one save (#1776)', async () => {
		serverPaths = [
			{ id: 0, name: 'Fire', description: 'd', activityKey: EActivityKey.Fire, retiredAt: null },
			{ id: 1, name: 'Water', description: 'd', activityKey: EActivityKey.Water, retiredAt: null }
		];
		serverProfs = [
			fullTier({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire T0' }),
			fullTier({ id: 1, pathId: 1, pathOrdinal: 0, name: 'Water T0', prerequisiteIds: [0] })
		];
		const store = new ProgressionStore();
		await store.load();

		// Retiring path 0 (Fire) would soft-lock the live gateway (Water T0) unless its prerequisite on
		// Fire T0 is also dropped in the same save.
		store.retirePath(0, true);
		store.removePrerequisite(1, 0);

		await store.save();

		expect(toastErrorMock).not.toHaveBeenCalled();
		expect(store.pathBaseline(0)?.retiredAt).toBeTruthy();
		expect(store.profBaseline(1)?.prerequisiteIds).toEqual([]);

		// The prerequisite removal must land before the path retire commits, or the backend's guard
		// (simulated above) would reject the retire against its still-live edge.
		const pathCallIndex = postMock.mock.calls.findIndex((c) => c[0] === 'AdminTools/AddEditPaths');
		const prereqCallIndex = postMock.mock.calls.findIndex((c) => c[0] === 'AdminTools/SetProficiencyPrerequisites');
		expect(prereqCallIndex).toBeGreaterThanOrEqual(0);
		expect(pathCallIndex).toBeGreaterThan(prereqCallIndex);
	});

	it('retiring a path that still gates a live gateway (prerequisite untouched) is rejected, same as before', async () => {
		serverPaths = [
			{ id: 0, name: 'Fire', description: 'd', activityKey: EActivityKey.Fire, retiredAt: null },
			{ id: 1, name: 'Water', description: 'd', activityKey: EActivityKey.Water, retiredAt: null }
		];
		serverProfs = [
			fullTier({ id: 0, pathId: 0, pathOrdinal: 0, name: 'Fire T0' }),
			fullTier({ id: 1, pathId: 1, pathOrdinal: 0, name: 'Water T0', prerequisiteIds: [0] })
		];
		const store = new ProgressionStore();
		await store.load();

		store.retirePath(0, true);

		await store.save();

		expect(toastErrorMock).toHaveBeenCalled();
		// Rejected pre-commit: nothing wrote, so the local retire edit is preserved for a clean retry.
		expect(store.paths.find((p) => p.id === 0)?.retiredAt).toBeTruthy();
		expect(serverPaths.find((p) => p.id === 0)?.retiredAt).toBeFalsy();
	});

	it('lowering MaxLevel and removing the now out-of-range payout in one save succeeds (#1827/#1804)', async () => {
		serverPaths = [{ id: 0, name: 'Fire', description: 'd', activityKey: EActivityKey.Fire, retiredAt: null }];
		serverProfs = [
			fullTier({
				id: 0,
				pathId: 0,
				pathOrdinal: 0,
				name: 'Fire T0',
				maxLevel: 10,
				levelModifiers: [{ level: 8, attributeId: 0, modifierTypeId: 0, amount: 5 }],
				levelRewards: [{ level: 8, rewardSkillId: 3 }]
			})
		];
		const store = new ProgressionStore();
		await store.load();

		store.patchProf(0, (d) => (d.maxLevel = 5));
		store.removePayout(0, 8);

		await store.save();

		expect(toastErrorMock).not.toHaveBeenCalled();
		expect(store.profBaseline(0)?.maxLevel).toBe(5);
		expect(store.profBaseline(0)?.levelModifiers).toHaveLength(0);
		expect(store.profBaseline(0)?.levelRewards).toHaveLength(0);

		// Both child collections' removals must land before the identity Edit commits, or the backend's
		// guard (simulated above) would reject the shrink against the still-persisted level-8 payout.
		const modifiersCallIndex = postMock.mock.calls.findIndex((c) => c[0] === 'AdminTools/SetProficiencyModifiers');
		const rewardsCallIndex = postMock.mock.calls.findIndex((c) => c[0] === 'AdminTools/SetProficiencyRewards');
		const identityCallIndex = postMock.mock.calls.findIndex((c) => c[0] === 'AdminTools/AddEditProficiencies');
		expect(modifiersCallIndex).toBeGreaterThanOrEqual(0);
		expect(rewardsCallIndex).toBeGreaterThanOrEqual(0);
		expect(identityCallIndex).toBeGreaterThan(modifiersCallIndex);
		expect(identityCallIndex).toBeGreaterThan(rewardsCallIndex);
	});

	it('lowering MaxLevel without removing the offending payout is still rejected, same as before', async () => {
		serverPaths = [{ id: 0, name: 'Fire', description: 'd', activityKey: EActivityKey.Fire, retiredAt: null }];
		serverProfs = [
			fullTier({
				id: 0,
				pathId: 0,
				pathOrdinal: 0,
				name: 'Fire T0',
				maxLevel: 10,
				levelModifiers: [{ level: 8, attributeId: 0, modifierTypeId: 0, amount: 5 }]
			})
		];
		const store = new ProgressionStore();
		await store.load();

		store.patchProf(0, (d) => (d.maxLevel = 5));

		await store.save();

		expect(toastErrorMock).toHaveBeenCalled();
		// Rejected pre-commit: nothing wrote, so the local edit is preserved for a clean retry.
		expect(store.profs.find((p) => p.id === 0)?.maxLevel).toBe(5);
		expect(serverProfs.find((p) => p.id === 0)?.maxLevel).toBe(10);
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
				activityKey: EActivityKey.Fire,
				retiredAt: null
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

		store.setPathTab('identity');
		expect(store.pathTab).toBe('identity');

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

	it('mutators no-op while a save is in flight so a keystroke landing mid-save cannot be silently discarded', async () => {
		seedServer();
		const store = new ProgressionStore();
		await store.load();

		store.saving = true;

		store.patchPath(0, (d) => (d.name = 'raced'));
		expect(reqPath(store, 0).name).toBe('Fire');

		store.patchProf(0, (d) => (d.name = 'raced'));
		expect(reqProf(store, 0).name).toBe('Fire T0');

		store.addPath();
		expect(store.paths).toHaveLength(1);

		const beforeTierCount = store.profs.length;
		expect(store.addTier(0)).toBe(0);
		expect(store.profs).toHaveLength(beforeTierCount);

		store.reorderTiers(0, 0, 0);
		store.removePath(0);
		expect(store.paths).toHaveLength(1);
		store.removeTier(0);
		expect(store.profs).toHaveLength(beforeTierCount);
	});
});
