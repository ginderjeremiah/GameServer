import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';

// The real AttributeChip is rendered (not stubbed) so the picker's controller-less surface is
// exercised: with no attribute-tooltip context here, the chips must stay out of the tab order.
import ClassPicker from '$routes/select/ClassPicker.svelte';
import { staticData } from '$stores/static-data.svelte';
import { EEquipmentSlot, type ICreatableClass } from '$lib/api';
import { makeCreatableClass } from '../../fixtures/classes';

/* The passive diverges from the fixture's inert default: the picker renders its attribute chip, so the
   class carries a real passive (Endurance +8) for those assertions to read. */
const cls = (overrides: Partial<ICreatableClass> = {}): ICreatableClass =>
	makeCreatableClass({
		id: 0,
		name: 'Warrior',
		description: 'A frontline fighter.',
		word: 'kor',
		passiveAttributeId: 1,
		passiveAmount: 8,
		...overrides
	});

beforeEach(() => {
	staticData.attributes = [{ id: 1, code: 'END', name: 'Endurance' }] as unknown as NonNullable<
		typeof staticData.attributes
	>;
});

afterEach(() => {
	cleanup();
	staticData.attributes = undefined;
});

describe('ClassPicker', () => {
	it('renders nothing when there are no classes (hide-on-empty)', () => {
		render(ClassPicker, { classes: [], selectedClassId: null, onSelect: vi.fn() });
		expect(screen.queryByTestId('class-picker')).toBeNull();
	});

	it('renders an option per class', () => {
		const classes = [cls({ id: 0, name: 'Warrior' }), cls({ id: 1, name: 'Mage' })];
		render(ClassPicker, { classes, selectedClassId: 0, onSelect: vi.fn() });

		expect(screen.getByTestId('class-option-0')).toBeTruthy();
		expect(screen.getByTestId('class-option-1')).toBeTruthy();
	});

	it('notifies the parent when an option is clicked', async () => {
		const classes = [cls({ id: 0, name: 'Warrior' }), cls({ id: 1, name: 'Mage' })];
		const onSelect = vi.fn();
		render(ClassPicker, { classes, selectedClassId: 0, onSelect });

		await fireEvent.click(screen.getByTestId('class-option-1'));
		expect(onSelect).toHaveBeenCalledWith(1);
	});

	it('previews the selected class kit: description, passive, skills, weapon-first equipment', () => {
		const classes = [
			cls({
				id: 0,
				description: 'A frontline fighter.',
				passiveAttributeId: 1,
				passiveAmount: 8,
				starterSkills: [
					{ id: 1, name: 'Slash' },
					{ id: 0, name: 'Punch' }
				],
				starterEquipment: [
					{ itemId: 0, equipmentSlot: EEquipmentSlot.ChestSlot, name: 'Rags' },
					{ itemId: 2, equipmentSlot: EEquipmentSlot.WeaponSlot, name: 'Iron Sword' }
				],
				attributeDistributions: [
					{ attributeId: 1, baseAmount: 10, amountPerLevel: 1 },
					{ attributeId: 2, baseAmount: 4, amountPerLevel: 0 }
				]
			})
		];
		const { container } = render(ClassPicker, { classes, selectedClassId: 0, onSelect: vi.fn() });

		expect(screen.getByTestId('class-preview')).toBeTruthy();
		expect(screen.getByText('A frontline fighter.')).toBeTruthy();
		expect(screen.getByTestId('class-passive').textContent).toBe('+8 END');
		expect(screen.getByText('Slash')).toBeTruthy();
		expect(screen.getByText('Punch')).toBeTruthy();
		// Weapon leads the equipment list (its name renders).
		expect(screen.getByText('Iron Sword')).toBeTruthy();
		// A fingerprint chip per attribute distribution.
		expect(container.querySelectorAll('.achip')).toHaveLength(2);
	});

	// The radiogroup roles promise the radio keyboard model: one tab stop for the whole group, with the
	// arrows moving between options (selecting as they go) rather than every option being a Tab stop.
	describe('radiogroup keyboard model', () => {
		const threeClasses = () => [
			cls({ id: 0, name: 'Warrior' }),
			cls({ id: 1, name: 'Mage' }),
			cls({ id: 2, name: 'Rogue' })
		];

		it('exposes exactly one tab stop — the selected option', () => {
			render(ClassPicker, { classes: threeClasses(), selectedClassId: 1, onSelect: vi.fn() });

			expect(screen.getByTestId('class-option-0').getAttribute('tabindex')).toBe('-1');
			expect(screen.getByTestId('class-option-1').getAttribute('tabindex')).toBe('0');
			expect(screen.getByTestId('class-option-2').getAttribute('tabindex')).toBe('-1');
		});

		it('falls back to the first option as the tab stop before anything is selected', () => {
			render(ClassPicker, { classes: threeClasses(), selectedClassId: null, onSelect: vi.fn() });

			expect(screen.getByTestId('class-option-0').getAttribute('tabindex')).toBe('0');
			expect(screen.getByTestId('class-option-1').getAttribute('tabindex')).toBe('-1');
		});

		it('selects and focuses the next option on ArrowRight/ArrowDown', async () => {
			const onSelect = vi.fn();
			render(ClassPicker, { classes: threeClasses(), selectedClassId: 0, onSelect });

			await fireEvent.keyDown(screen.getByTestId('class-option-0'), { key: 'ArrowRight' });
			expect(onSelect).toHaveBeenCalledWith(1);
			expect(document.activeElement).toBe(screen.getByTestId('class-option-1'));

			await fireEvent.keyDown(screen.getByTestId('class-option-0'), { key: 'ArrowDown' });
			expect(onSelect).toHaveBeenLastCalledWith(1);
		});

		it('wraps around the group in both directions', async () => {
			const onSelect = vi.fn();
			render(ClassPicker, { classes: threeClasses(), selectedClassId: 0, onSelect });

			await fireEvent.keyDown(screen.getByTestId('class-option-0'), { key: 'ArrowLeft' });
			expect(onSelect).toHaveBeenLastCalledWith(2);

			await fireEvent.keyDown(screen.getByTestId('class-option-2'), { key: 'ArrowRight' });
			expect(onSelect).toHaveBeenLastCalledWith(0);
		});

		it('jumps to the first/last option on Home/End', async () => {
			const onSelect = vi.fn();
			render(ClassPicker, { classes: threeClasses(), selectedClassId: 1, onSelect });

			await fireEvent.keyDown(screen.getByTestId('class-option-1'), { key: 'End' });
			expect(onSelect).toHaveBeenLastCalledWith(2);

			await fireEvent.keyDown(screen.getByTestId('class-option-1'), { key: 'Home' });
			expect(onSelect).toHaveBeenLastCalledWith(0);
		});

		it('leaves other keys to their native handling', async () => {
			const onSelect = vi.fn();
			render(ClassPicker, { classes: threeClasses(), selectedClassId: 0, onSelect });

			// Space/Enter activate the real <button> natively, so the handler must not intercept them.
			await fireEvent.keyDown(screen.getByTestId('class-option-0'), { key: 'Tab' });
			await fireEvent.keyDown(screen.getByTestId('class-option-0'), { key: ' ' });
			expect(onSelect).not.toHaveBeenCalled();
		});
	});

	it('keeps the fingerprint chips out of the tab order (no tooltip controller on this surface)', () => {
		const classes = [
			cls({
				id: 0,
				attributeDistributions: [
					{ attributeId: 1, baseAmount: 10, amountPerLevel: 1 },
					{ attributeId: 2, baseAmount: 4, amountPerLevel: 0 }
				]
			})
		];
		const { container } = render(ClassPicker, { classes, selectedClassId: 0, onSelect: vi.fn() });

		// The picker publishes no attribute tooltip, so a focusable chip would be a dead tab stop.
		const chips = [...container.querySelectorAll('.achip')];
		expect(chips).toHaveLength(2);
		for (const chip of chips) {
			expect(chip.hasAttribute('tabindex')).toBe(false);
		}
	});
});
