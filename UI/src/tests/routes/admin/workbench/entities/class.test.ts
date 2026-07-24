import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EAttribute, EEquipmentSlot, EItemCategory, EModifierType, ESkillAcquisition } from '$lib/api';
import type { ChipsSectionConfig, TableSectionConfig } from '$routes/admin/workbench/entities/types';

/** A non-core, derived attribute — any id past the six-member core set works for the restriction tests. */
const NON_CORE_ATTRIBUTE = EAttribute.MaxHealth;

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

/** The starter-skills chips section, for exercising its Player-flag `warn` predicate. */
const starterSkillsSection = () =>
	classEntity.sections.find((s) => s.key === 'starterSkills') as ChipsSectionConfig<WorkbenchClass>;

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

	it('warn flags a slot/category mismatch as a backstop, blocking Save (backend-enforced)', () => {
		const cls = blankClass([{ equipmentSlot: EEquipmentSlot.HelmSlot, itemId: 1 }]);
		expect(starterEquipmentSection().warn?.(cls)).toEqual({
			message: "Item doesn't match its equipment slot's category",
			blocking: true
		});
	});
});

describe('starterSkills chips section (#2333)', () => {
	beforeEach(() => {
		staticData.skills = [
			{ id: 0, name: 'Slash', acquisition: ESkillAcquisition.Player },
			{ id: 1, name: 'Bite', acquisition: ESkillAcquisition.Enemy } // flag stripped after assignment
		];
	});

	it('warn flags a starter skill that lost its Player flag, blocking Save (backend-enforced)', () => {
		const cls: WorkbenchClass = { ...classEntity.newItem(0), starterSkillIds: [0, 1] };
		expect(starterSkillsSection().warn?.(cls)).toEqual({
			message: "'Bite' is no longer flagged as Player-acquirable",
			blocking: true
		});
	});

	it('warn is null when every starter skill is still Player-flagged', () => {
		const cls: WorkbenchClass = { ...classEntity.newItem(0), starterSkillIds: [0] };
		expect(starterSkillsSection().warn?.(cls)).toBeNull();
	});

	it('warn falls back to the advisory "no starter skills" nag when the list is empty', () => {
		expect(starterSkillsSection().warn?.(classEntity.newItem(0))).toBe('No starter skills');
	});
});

describe('attributes distribution section (#2376)', () => {
	const attributesSection = () =>
		classEntity.sections.find((s) => s.key === 'attributes') as TableSectionConfig<WorkbenchClass>;

	it("the attribute picker offers only core attributes, unlike an enemy's unrestricted one", () => {
		const options = attributesSection().columns[0].options?.();
		expect(options?.some((o) => o.value === NON_CORE_ATTRIBUTE)).toBe(false);
		expect(options?.some((o) => o.value === EAttribute.Strength)).toBe(true);
	});

	it('warns a distribution row on a non-core attribute, blocking Save (backend-enforced)', () => {
		const cls: WorkbenchClass = {
			...classEntity.newItem(0),
			attributeDistributions: [{ attributeId: NON_CORE_ATTRIBUTE, baseAmount: 1, amountPerLevel: 0 }]
		};
		expect(attributesSection().warn?.(cls)).toMatchObject({ blocking: true });
	});

	it('does not warn a distribution made up entirely of core attributes', () => {
		const cls: WorkbenchClass = {
			...classEntity.newItem(0),
			attributeDistributions: [{ attributeId: EAttribute.Strength, baseAmount: 1, amountPerLevel: 0 }]
		};
		expect(attributesSection().warn?.(cls)).toBeNull();
	});
});
