import { describe, it, expect, beforeEach, vi } from 'vitest';

const { dangerModal, staticData } = vi.hoisted(() => ({
	dangerModal: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ dangerModal, staticData }));

import { referenceSourcesFromStatic, retireWithConfirm } from '$routes/admin/workbench/retire-confirm';
import type { ReferenceSources } from '$routes/admin/workbench/references';

const emptySources: ReferenceSources = {
	enemies: [],
	zones: [],
	challenges: [],
	items: [],
	classes: [],
	skillRecipes: [],
	proficiencies: [],
	skills: []
};

beforeEach(() => {
	dangerModal.mockReset();
	for (const key of ['enemies', 'zones', 'challenges', 'items', 'classes', 'skillRecipes', 'proficiencies', 'skills']) {
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
