// @vitest-environment jsdom
import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EAttribute } from '$lib/api';

const controller = { describedById: 'tooltip-1', show: vi.fn(), move: vi.fn(), hide: vi.fn() };

// The chip resolves the screen-level controller via context; the action/controller wiring is
// covered elsewhere, so here we stub it to assert the chip drives it on hover and focus.
vi.mock('$components/tooltip/attribute-tooltip.svelte', () => ({
	getAttributeTooltip: () => controller
}));

vi.mock('$stores', () => ({
	staticData: { attributes: [{ id: EAttribute.Strength, code: 'STR', name: 'Strength' }] }
}));

import AttributeChip from '$components/AttributeChip.svelte';

afterEach(() => {
	cleanup();
	vi.clearAllMocks();
});

describe('AttributeChip', () => {
	it('renders the attribute code, icon, and colour tint', () => {
		const { container } = render(AttributeChip, { props: { attributeId: EAttribute.Strength } });
		const chip = container.querySelector('.achip') as HTMLElement;
		expect(chip.textContent).toContain('STR');
		expect(chip.querySelector('img.attr-icon')).toBeTruthy();
		expect(chip.style.getPropertyValue('--ac')).toBe('var(--attr-strength)');
	});

	it('applies the wide modifier only when requested', () => {
		const plain = render(AttributeChip, { props: { attributeId: EAttribute.Strength } });
		expect(plain.container.querySelector('.achip')?.classList.contains('wide')).toBe(false);
		cleanup();
		const wide = render(AttributeChip, { props: { attributeId: EAttribute.Strength, wide: true } });
		expect(wide.container.querySelector('.achip')?.classList.contains('wide')).toBe(true);
	});

	it('is keyboard-reachable with tabindex, role, and an aria-label', () => {
		const { container } = render(AttributeChip, { props: { attributeId: EAttribute.Strength } });
		const chip = container.querySelector('.achip') as HTMLElement;
		expect(chip.getAttribute('tabindex')).toBe('0');
		expect(chip.getAttribute('role')).toBe('img');
		expect(chip.getAttribute('aria-label')).toBe('Strength');
	});

	it('drives the shared attribute tooltip across hover (enter/move/leave)', async () => {
		const { container } = render(AttributeChip, { props: { attributeId: EAttribute.Strength } });
		const chip = container.querySelector('.achip') as HTMLElement;
		await fireEvent.mouseEnter(chip);
		expect(controller.show).toHaveBeenCalledWith(EAttribute.Strength, expect.anything());
		await fireEvent.mouseMove(chip);
		expect(controller.move).toHaveBeenCalled();
		await fireEvent.mouseLeave(chip);
		expect(controller.hide).toHaveBeenCalled();
	});

	it('opens the tooltip on focus and closes on blur', async () => {
		const { container } = render(AttributeChip, { props: { attributeId: EAttribute.Strength } });
		const chip = container.querySelector('.achip') as HTMLElement;
		await fireEvent.focus(chip);
		expect(controller.show).toHaveBeenCalledWith(EAttribute.Strength, chip);
		await fireEvent.blur(chip);
		expect(controller.hide).toHaveBeenCalled();
	});

	it('wires the shared tooltip id onto aria-describedby so the explanation is announced on focus', () => {
		const { container } = render(AttributeChip, { props: { attributeId: EAttribute.Strength } });
		expect(container.querySelector('.achip')!.getAttribute('aria-describedby')).toBe('tooltip-1');
	});
});
