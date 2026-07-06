import { describe, it, expect, vi } from 'vitest';
import { EAttribute, EModifierType } from '$lib/api';

/* Only the refresh() write-through added for #1633 — classEntity's field/table configs aren't touched here.
   `fetchSocketData` is stubbed; the write-through into staticData.classes is what retire-confirm's reference
   computation (starter-skill/starter-equipment groups) reads. */

const { staticData, socket, mockFetch } = vi.hoisted(() => {
	const socket = { classes: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockFetch: vi.fn(async (command: string) => (command === 'GetClasses' ? socket.classes : []))
	};
});

vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static post = vi.fn();
		static get = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetch };
});

import { classEntity } from '$routes/admin/workbench/entities/class';

describe('classEntity.refresh', () => {
	it('writes the fetched classes through to staticData.classes (#1633 — needed for retire-confirm)', async () => {
		socket.classes = [
			{
				id: 0,
				name: 'Warrior',
				description: '',
				word: '',
				passiveAttributeId: EAttribute.Strength,
				passiveAmount: 1,
				passiveScalingAmount: 0,
				passiveModifierType: EModifierType.Additive,
				designerNotes: '',
				starterSkillIds: [3],
				starterEquipment: [],
				attributeDistributions: []
			}
		];

		const result = await classEntity.refresh();

		expect(staticData.classes).toBe(result);
		expect(staticData.classes[0]).toMatchObject({ name: 'Warrior', starterSkillIds: [3] });
	});

	it('normalises a missing scaling attribute to the -1 sentinel in the written-through copy', async () => {
		socket.classes = [
			{
				id: 0,
				name: 'Mage',
				description: '',
				word: '',
				passiveAttributeId: EAttribute.Intellect,
				passiveAmount: 1,
				passiveScalingAmount: 0,
				passiveModifierType: EModifierType.Additive,
				designerNotes: '',
				starterSkillIds: [],
				starterEquipment: [],
				attributeDistributions: []
			}
		];

		await classEntity.refresh();

		expect(staticData.classes[0].passiveScalingAttributeId).toBe(-1);
	});
});
