// @vitest-environment jsdom
import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { tick } from 'svelte';
import { EAttribute, EAttributeType, EDamageTypeKey } from '$lib/api';
import { makeAttribute } from '../../../../fixtures/attributes';

// The picker reads the attribute reference set from the shared staticData store for its grouping;
// mock it as a mutable object (populated in beforeEach) so the factory stays hoist-safe.
const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ staticData }));

import AttributePicker from '$routes/admin/workbench/components/AttributePicker.svelte';
import type { SelectOption } from '$routes/admin/workbench/entities/types';

const ATTRIBUTES = [
	makeAttribute(EAttribute.Strength, 'Strength', { attributeType: EAttributeType.Primary, displayOrder: 0 }),
	makeAttribute(EAttribute.MaxHealth, 'Max Health', { attributeType: EAttributeType.Secondary, displayOrder: 6 }),
	makeAttribute(EAttribute.FireAmplification, 'Fire Amplification', {
		attributeType: EAttributeType.Affinity,
		displayOrder: 19,
		damageTypeKey: EDamageTypeKey.Fire
	})
];
beforeEach(() => {
	staticData.attributes = ATTRIBUTES;
});

const OPTIONS: SelectOption[] = [
	{ value: EAttribute.Strength, text: 'Strength' },
	{ value: EAttribute.MaxHealth, text: 'Max Health' },
	{ value: EAttribute.FireAmplification, text: 'Fire Amplification' }
];

const setup = (over: { value?: number; disabledValues?: Set<number> } = {}) => {
	const onChange = vi.fn();
	const result = render(AttributePicker, {
		props: { value: EAttribute.Strength, options: OPTIONS, onChange, ariaLabel: 'Attribute', ...over }
	});
	return { ...result, onChange };
};

const openPanel = async (container: HTMLElement) => {
	await fireEvent.click(container.querySelector('.attr-trigger') as HTMLElement);
	await tick();
};

afterEach(cleanup);

describe('AttributePicker', () => {
	it('shows the current value on the trigger and no panel until opened', () => {
		const { container } = setup();
		expect((container.querySelector('.attr-trigger-text') as HTMLElement).textContent).toBe('Strength');
		expect(container.querySelector('.attr-panel')).toBeNull();
		expect(container.querySelector('.attr-trigger')?.getAttribute('aria-expanded')).toBe('false');
	});

	it('opens a grouped, searchable panel and selects an option', async () => {
		const { container, onChange } = setup();
		await openPanel(container);

		expect(container.querySelector('.attr-panel')).not.toBeNull();
		// The trigger announces the open state for assistive tech.
		expect(container.querySelector('.attr-trigger')?.getAttribute('aria-expanded')).toBe('true');
		// Grouped by taxonomy, so the band headers are present.
		const heads = [...container.querySelectorAll('.attr-group-head')].map((h) => h.textContent?.trim());
		expect(heads).toContain('Primary');
		expect(heads).toContain('Fire');

		const options = [...container.querySelectorAll('.attr-opt')];
		const maxHealth = options.find((o) => o.textContent?.includes('Max Health')) as HTMLElement;
		await fireEvent.click(maxHealth);
		expect(onChange).toHaveBeenCalledWith(EAttribute.MaxHealth);
		// Selecting closes the panel.
		expect(container.querySelector('.attr-panel')).toBeNull();
	});

	it('filters options by the search text', async () => {
		const { container } = setup();
		await openPanel(container);
		await fireEvent.input(container.querySelector('.attr-search-inp') as HTMLInputElement, {
			target: { value: 'fire' }
		});
		const visible = [...container.querySelectorAll('.attr-opt')].map((o) => o.textContent?.trim());
		expect(visible).toEqual(['Fire Amplification']);
	});

	it('selects the first match on Enter in the search box', async () => {
		const { container, onChange } = setup();
		await openPanel(container);
		const search = container.querySelector('.attr-search-inp') as HTMLInputElement;
		await fireEvent.input(search, { target: { value: 'max' } });
		await fireEvent.keyDown(search, { key: 'Enter' });
		expect(onChange).toHaveBeenCalledWith(EAttribute.MaxHealth);
	});

	it('disables a value taken by a sibling row but never the current value', async () => {
		const { container, onChange } = setup({
			disabledValues: new Set([EAttribute.MaxHealth, EAttribute.Strength])
		});
		await openPanel(container);
		const options = [...container.querySelectorAll('.attr-opt')] as HTMLButtonElement[];
		const maxHealth = options.find((o) => o.textContent?.includes('Max Health'))!;
		const strength = options.find((o) => o.textContent?.includes('Strength'))!;
		// Max Health is taken elsewhere → disabled; Strength is the current value → still selectable.
		expect(maxHealth.disabled).toBe(true);
		expect(strength.disabled).toBe(false);
		await fireEvent.click(maxHealth);
		expect(onChange).not.toHaveBeenCalled();
	});
});
