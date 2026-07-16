import { describe, it, expect, beforeEach, vi } from 'vitest';

const { dangerModal, staticData } = vi.hoisted(() => ({
	dangerModal: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ dangerModal, staticData }));

import {
	deleteWithConfirm,
	ownCatalogueOverride,
	referenceSourcesFromStatic,
	retireWithConfirm
} from '$routes/admin/workbench/retire-confirm';
import { computeReferences } from '$routes/admin/workbench/references';
import type { ReferenceSources } from '$routes/admin/workbench/references';

const emptySources: ReferenceSources = {
	enemies: [],
	zones: [],
	challenges: [],
	items: [],
	itemMods: [],
	classes: [],
	skillRecipes: [],
	proficiencies: [],
	skills: []
};

beforeEach(() => {
	dangerModal.mockReset();
	for (const key of [
		'enemies',
		'zones',
		'challenges',
		'items',
		'itemMods',
		'classes',
		'skillRecipes',
		'proficiencies',
		'skills'
	]) {
		delete staticData[key];
	}
});

describe('referenceSourcesFromStatic', () => {
	it('falls back to an empty array for every slot before staticData has loaded', () => {
		expect(referenceSourcesFromStatic()).toEqual(emptySources);
	});

	it('passes through whatever staticData currently holds for each catalogue', () => {
		staticData.enemies = [{ id: 0, name: 'Cave Bat' }];
		staticData.skills = [{ id: 0, name: 'Fire Bolt' }];

		const sources = referenceSourcesFromStatic();
		expect(sources.enemies).toBe(staticData.enemies);
		expect(sources.skills).toBe(staticData.skills);
		expect(sources.zones).toEqual([]);
	});

	it('substitutes an override for the one catalogue the caller is live-editing, leaving the rest on staticData (#1863)', () => {
		staticData.enemies = [{ id: 0, name: 'Cave Bat (last saved)' }];
		staticData.proficiencies = [{ id: 0, name: 'Blades (last saved)' }];
		const liveProficiencies = [
			{ id: 0, name: 'Blades (unsaved edit)' }
		] as unknown as ReferenceSources['proficiencies'];

		const sources = referenceSourcesFromStatic({ proficiencies: liveProficiencies });

		expect(sources.proficiencies).toBe(liveProficiencies);
		expect(sources.enemies).toBe(staticData.enemies);
	});
});

describe('ownCatalogueOverride (#1976)', () => {
	it('is a no-op for an entity whose reference lookup never reads its own catalogue', () => {
		const items = [{ id: 0, name: 'Verdant Hollow' }];
		expect(ownCatalogueOverride('zones', items)).toEqual({});
	});

	it('rebuilds a dense-by-id enemies array from live store items, ignoring never-saved (negative-id) additions', () => {
		// EntityStore.addItem prepends new records, so a session that added one enemy before retiring
		// an original one sees array position 0 hold the new (negative-id) enemy, not id 0.
		const liveItems = [
			{ id: -1, name: 'New Enemy (unsaved)', spawns: [{ zoneId: 9, weight: 1 }] },
			{ id: 0, name: 'Cave Bat', spawns: [{ zoneId: 2, weight: 7 }] },
			{ id: 1, name: 'Catacomb Lich', spawns: [] }
		];

		const override = ownCatalogueOverride('enemies', liveItems);

		expect(override.enemies?.[0]).toEqual(liveItems[1]);
		expect(override.enemies?.[1]).toEqual(liveItems[2]);
		expect(override.enemies?.[-1]).toBeUndefined();
	});

	it("reflects this session's edited spawns when computing an enemy's own referenced-by surface", () => {
		staticData.enemies = [{ id: 0, name: 'Cave Bat (last saved)', spawns: [] }];
		// staticData is server-dense (zero-based-id invariant), so index 0 must resolve to id 0 too.
		staticData.zones = [{ id: 0, name: 'Sunken Ruins' }];
		const liveEnemies = [
			{ id: -1, name: 'New Enemy (unsaved)', spawns: [] },
			// This session added a spawn row not yet saved — staticData still shows an empty spawn list.
			{ id: 0, name: 'Cave Bat', spawns: [{ zoneId: 0, weight: 4 }] }
		];

		const sources = referenceSourcesFromStatic(ownCatalogueOverride('enemies', liveEnemies));
		const groups = computeReferences('enemies', 0, sources);

		expect(groups.find((g) => g.kind === 'spawnsIn')?.names).toEqual(['Sunken Ruins']);
	});
});

describe('retireWithConfirm', () => {
	it('retires immediately, without prompting, when nothing references the record', async () => {
		const onConfirmed = vi.fn();
		await retireWithConfirm({
			entityKey: 'enemies',
			id: 0,
			name: 'Cave Bat',
			title: 'Retire Enemy?',
			sources: emptySources,
			onConfirmed
		});

		expect(dangerModal).not.toHaveBeenCalled();
		expect(onConfirmed).toHaveBeenCalledOnce();
	});

	it('prompts with the referenced-by body and retires only when confirmed', async () => {
		dangerModal.mockResolvedValue(true);
		const onConfirmed = vi.fn();
		const sources: ReferenceSources = {
			...emptySources,
			zones: [{ id: 0, name: 'Frost Cavern', bossEnemyId: 1 }] as unknown as ReferenceSources['zones']
		};

		await retireWithConfirm({
			entityKey: 'enemies',
			id: 1,
			name: 'Catacomb Lich',
			title: 'Retire Enemy?',
			sources,
			onConfirmed
		});

		expect(dangerModal).toHaveBeenCalledOnce();
		const call = dangerModal.mock.calls[0][0];
		expect(call.title).toBe('Retire Enemy?');
		expect(call.body).toContain('Frost Cavern');
		expect(onConfirmed).toHaveBeenCalledOnce();
	});

	it('does not retire when the confirm dialog is cancelled', async () => {
		dangerModal.mockResolvedValue(false);
		const onConfirmed = vi.fn();
		const sources: ReferenceSources = {
			...emptySources,
			zones: [{ id: 0, name: 'Frost Cavern', bossEnemyId: 1 }] as unknown as ReferenceSources['zones']
		};

		await retireWithConfirm({
			entityKey: 'enemies',
			id: 1,
			name: 'Catacomb Lich',
			title: 'Retire Enemy?',
			sources,
			onConfirmed
		});

		expect(dangerModal).toHaveBeenCalledOnce();
		expect(onConfirmed).not.toHaveBeenCalled();
	});

	it('surfaces a gear-gate/recipe-condition/prerequisite reference when retiring a proficiency', async () => {
		dangerModal.mockResolvedValue(true);
		const onConfirmed = vi.fn();
		const sources: ReferenceSources = {
			...emptySources,
			items: [{ id: 0, name: 'Iron Helm', requiredProficiencyId: 5 }] as unknown as ReferenceSources['items'],
			proficiencies: [
				{ id: 6, name: 'Advanced Blades', prerequisiteIds: [5] }
			] as unknown as ReferenceSources['proficiencies']
		};

		await retireWithConfirm({
			entityKey: 'proficiencies',
			id: 5,
			name: 'Blades',
			title: 'Retire tier?',
			sources,
			onConfirmed
		});

		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Iron Helm');
		expect(body).toContain('Advanced Blades');
		expect(onConfirmed).toHaveBeenCalledOnce();
	});

	it('surfaces a class starter-skill reference from a bare admin load (#1633 — classes must land in staticData)', async () => {
		dangerModal.mockResolvedValue(true);
		const onConfirmed = vi.fn();
		// Simulates reference.load() populating staticData.classes on a direct /admin entry (no game load).
		staticData.classes = [{ id: 0, name: 'Warrior', starterSkillIds: [3], starterEquipment: [] }];

		await retireWithConfirm({
			entityKey: 'skills',
			id: 3,
			name: 'Cleave',
			title: 'Retire Skill?',
			sources: referenceSourcesFromStatic(),
			onConfirmed
		});

		expect(dangerModal).toHaveBeenCalledOnce();
		const body = dangerModal.mock.calls[0][0].body as string;
		expect(body).toContain('Warrior');
		expect(onConfirmed).toHaveBeenCalledOnce();
	});
});

describe('deleteWithConfirm', () => {
	it('deletes immediately, without prompting, when nothing carries the tag', async () => {
		const onConfirmed = vi.fn();
		await deleteWithConfirm({
			entityKey: 'tags',
			id: 10,
			name: 'Fire',
			title: 'Delete Tag?',
			sources: emptySources,
			onConfirmed
		});

		expect(dangerModal).not.toHaveBeenCalled();
		expect(onConfirmed).toHaveBeenCalledOnce();
	});

	it('prompts with the applied-to body under a "Delete anyway" label and deletes only when confirmed', async () => {
		dangerModal.mockResolvedValue(true);
		const onConfirmed = vi.fn();
		const sources: ReferenceSources = {
			...emptySources,
			items: [{ id: 0, name: 'Iron Helm', tags: [10] }] as unknown as ReferenceSources['items']
		};

		await deleteWithConfirm({
			entityKey: 'tags',
			id: 10,
			name: 'Fire',
			title: 'Delete Tag?',
			sources,
			onConfirmed
		});

		expect(dangerModal).toHaveBeenCalledOnce();
		const call = dangerModal.mock.calls[0][0];
		expect(call.title).toBe('Delete Tag?');
		expect(call.confirmLabel).toBe('Delete anyway');
		expect(call.body).toContain('Iron Helm');
		expect(onConfirmed).toHaveBeenCalledOnce();
	});

	it('does not delete when the confirm dialog is cancelled', async () => {
		dangerModal.mockResolvedValue(false);
		const onConfirmed = vi.fn();
		const sources: ReferenceSources = {
			...emptySources,
			itemMods: [{ id: 0, name: 'Sharp', tags: [10] }] as unknown as ReferenceSources['itemMods']
		};

		await deleteWithConfirm({
			entityKey: 'tags',
			id: 10,
			name: 'Fire',
			title: 'Delete Tag?',
			sources,
			onConfirmed
		});

		expect(dangerModal).toHaveBeenCalledOnce();
		expect(onConfirmed).not.toHaveBeenCalled();
	});
});
