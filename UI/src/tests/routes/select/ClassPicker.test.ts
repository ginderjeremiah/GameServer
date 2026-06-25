import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';

// AttributeChip is stubbed to a countable marker so fingerprint rendering can be asserted without the
// icon/tooltip machinery.
vi.mock('$components/AttributeChip.svelte', () => ({ default: ChipStub }));
import ChipStub from '../game/screens/skills/AttributeChipStub.svelte';

import ClassPicker from '$routes/select/ClassPicker.svelte';
import { staticData } from '$stores/static-data.svelte';
import { EEquipmentSlot, EModifierType, type IClass } from '$lib/api';

const cls = (overrides: Partial<IClass> = {}): IClass =>
	({
		id: 0,
		name: 'Warrior',
		description: 'A frontline fighter.',
		word: 'kor',
		passiveAttributeId: 1,
		passiveAmount: 8,
		passiveScalingAmount: 0,
		passiveModifierType: EModifierType.Additive,
		starterSkillIds: [],
		starterEquipment: [],
		attributeDistributions: [],
		...overrides
	}) as IClass;

beforeEach(() => {
	staticData.classes = undefined;
	// Index-addressable by id (index === id), the catalogue lookup convention.
	staticData.skills = [
		{ id: 0, name: 'Punch' },
		{ id: 1, name: 'Slash' }
	] as unknown as NonNullable<typeof staticData.skills>;
	staticData.items = [
		{ id: 0, name: 'Rags' },
		{ id: 1, name: 'Boots' },
		{ id: 2, name: 'Iron Sword' }
	] as unknown as NonNullable<typeof staticData.items>;
	staticData.attributes = [{ id: 1, code: 'END', name: 'Endurance' }] as unknown as NonNullable<
		typeof staticData.attributes
	>;
});

afterEach(() => {
	cleanup();
	staticData.classes = undefined;
	staticData.skills = undefined;
	staticData.items = undefined;
	staticData.attributes = undefined;
});

describe('ClassPicker', () => {
	it('shows an empty message when no classes are available', () => {
		render(ClassPicker, { selectedClassId: null, onSelect: vi.fn() });
		expect(screen.getByTestId('class-picker-empty')).toBeTruthy();
	});

	it('renders an option per active class and excludes retired ones', () => {
		staticData.classes = [
			cls({ id: 0, name: 'Warrior' }),
			cls({ id: 1, name: 'Mage', retiredAt: '2026-01-01T00:00:00Z' })
		];
		render(ClassPicker, { selectedClassId: 0, onSelect: vi.fn() });

		expect(screen.getByTestId('class-option-0')).toBeTruthy();
		expect(screen.queryByTestId('class-option-1')).toBeNull();
	});

	it('defaults to the first available class once classes load', () => {
		staticData.classes = [cls({ id: 4, name: 'Warrior' }), cls({ id: 5, name: 'Mage' })];
		const onSelect = vi.fn();
		render(ClassPicker, { selectedClassId: null, onSelect });

		expect(onSelect).toHaveBeenCalledWith(4);
	});

	it('notifies the parent when an option is clicked', async () => {
		staticData.classes = [cls({ id: 0, name: 'Warrior' }), cls({ id: 1, name: 'Mage' })];
		const onSelect = vi.fn();
		render(ClassPicker, { selectedClassId: 0, onSelect });

		await fireEvent.click(screen.getByTestId('class-option-1'));
		expect(onSelect).toHaveBeenCalledWith(1);
	});

	it('previews the selected class kit: description, passive, skills, weapon-first equipment', () => {
		staticData.classes = [
			cls({
				id: 0,
				description: 'A frontline fighter.',
				passiveAttributeId: 1,
				passiveAmount: 8,
				starterSkillIds: [1, 0],
				starterEquipment: [
					{ itemId: 0, equipmentSlot: EEquipmentSlot.ChestSlot },
					{ itemId: 2, equipmentSlot: EEquipmentSlot.WeaponSlot }
				],
				attributeDistributions: [
					{ attributeId: 1, baseAmount: 10, amountPerLevel: 1 },
					{ attributeId: 2, baseAmount: 4, amountPerLevel: 0 }
				]
			})
		];
		render(ClassPicker, { selectedClassId: 0, onSelect: vi.fn() });

		expect(screen.getByTestId('class-preview')).toBeTruthy();
		expect(screen.getByText('A frontline fighter.')).toBeTruthy();
		expect(screen.getByTestId('class-passive').textContent).toBe('+8 END');
		expect(screen.getByText('Slash')).toBeTruthy();
		expect(screen.getByText('Punch')).toBeTruthy();
		// Weapon leads the equipment list.
		expect(screen.getByText('Iron Sword')).toBeTruthy();
		// A fingerprint chip per attribute distribution.
		expect(screen.getAllByTestId('attr-chip')).toHaveLength(2);
	});
});
