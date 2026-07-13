import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EEquipmentSlot, EItemCategory, EModifierType } from '$lib/api';
import type { TableSectionConfig } from '$routes/admin/workbench/entities/types';

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

import { classEntity, type WorkbenchClass } from '$routes/admin/workbench/entities/class';

/** The starter-equipment table section, for exercising its `newRow`/column-options/`warn` (#1782). */
const starterEquipmentSection = () =>
	classEntity.sections.find((s) => s.key === 'starterEquipment') as TableSectionConfig<WorkbenchClass>;

const blankClass = (starterEquipment: WorkbenchClass['starterEquipment'] = []): WorkbenchClass => ({
	...classEntity.newItem(0),
	starterEquipment
});

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

		await classEntity.refresh();

		expect(staticData.classes).toBe(socket.classes);
		expect(staticData.classes[0]).toMatchObject({ name: 'Warrior', starterSkillIds: [3] });
	});

	it('normalises a missing scaling attribute to the -1 sentinel only in the returned editable copy, keeping staticData.classes raw', async () => {
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

		const result = await classEntity.refresh();

		expect(result[0].passiveScalingAttributeId).toBe(-1);
		expect(staticData.classes[0].passiveScalingAttributeId).toBeUndefined();
	});
});

describe('starterEquipment section (#1782)', () => {
	beforeEach(() => {
		staticData.items = [
			{ id: 0, name: 'Iron Helm', itemCategoryId: EItemCategory.Helm },
			{ id: 1, name: 'Dragon Blade', itemCategoryId: EItemCategory.Weapon }
		];
	});

	it('newRow picks the first item matching the auto-picked slot, not just the first catalogue item', () => {
		// The first item option overall is Iron Helm (Helm); a Weapon-slot row must not default to it.
		const row = starterEquipmentSection().newRow(blankClass());
		expect(row.equipmentSlot).toBe(EEquipmentSlot.HelmSlot);
		expect(row.itemId).toBe(0);
	});

	it("newRow skips to the next free slot and picks that slot's matching item", () => {
		const row = starterEquipmentSection().newRow(blankClass([{ equipmentSlot: EEquipmentSlot.HelmSlot, itemId: 0 }]));
		expect(row.equipmentSlot).toBe(EEquipmentSlot.ChestSlot);
		// No Chest item in the catalogue, so newRow falls back to 0 (no valid option).
		expect(row.itemId).toBe(0);
	});

	it("the Item column's options are filtered to the row's slot category", () => {
		const itemColumn = starterEquipmentSection().columns.find((c) => c.key === 'itemId')!;
		expect(itemColumn.options?.(undefined, { equipmentSlot: EEquipmentSlot.HelmSlot })).toEqual([
			{ value: 0, text: 'Iron Helm' }
		]);
		expect(itemColumn.options?.(undefined, { equipmentSlot: EEquipmentSlot.WeaponSlot })).toEqual([
			{ value: 1, text: 'Dragon Blade' }
		]);
	});

	it('warn is null when every entry matches its slot category', () => {
		const cls = blankClass([
			{ equipmentSlot: EEquipmentSlot.HelmSlot, itemId: 0 },
			{ equipmentSlot: EEquipmentSlot.WeaponSlot, itemId: 1 }
		]);
		expect(starterEquipmentSection().warn?.(cls)).toBeNull();
	});

	it('warn flags a slot/category mismatch as a backstop', () => {
		const cls = blankClass([{ equipmentSlot: EEquipmentSlot.HelmSlot, itemId: 1 }]);
		expect(starterEquipmentSection().warn?.(cls)).toBe("Item doesn't match its equipment slot's category");
	});
});
