import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EAttributeType, EModifierType, type IAttribute } from '$lib/api';
import { makeAttribute } from '../../fixtures/attributes';

// The tooltip resolves the attribute's name/type/description/harmful flag from the reference store.
const { staticData } = vi.hoisted(() => ({ staticData: { attributes: [] as IAttribute[] | undefined } }));
vi.mock('$stores', () => ({ staticData }));

import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';

const STRENGTH = makeAttribute(EAttribute.Strength, 'Strength', {
	attributeType: EAttributeType.Primary,
	description: 'Raw physical power.'
});
const DEFENSE = makeAttribute(EAttribute.Defense, 'Defense', {
	attributeType: EAttributeType.Secondary,
	description: 'Reduces incoming damage.'
});
const DOT = makeAttribute(EAttribute.DamageTakenPerSecond, 'Damage Taken Per Second', {
	attributeType: EAttributeType.Status,
	description: 'Bleeding out.',
	isHarmful: true
});

afterEach(cleanup);

describe('AttributeTooltip', () => {
	it('renders nothing for an undefined attribute (the empty/hidden panel)', () => {
		staticData.attributes = [STRENGTH];
		const { container } = render(AttributeTooltip, { props: { attributeId: undefined } });
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});

	it('assembles the icon, name, type and description from the reference data', () => {
		staticData.attributes = [STRENGTH];
		const { container } = render(AttributeTooltip, { props: { attributeId: EAttribute.Strength } });

		// Icon (the leading art in the title), name, Primary/Secondary/Status type, and description.
		expect(container.querySelector('img.attr-icon')?.getAttribute('src')).toBe('/img/Strength.png');
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Strength');
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Primary');
		expect((container.querySelector('.at-desc') as HTMLElement).textContent).toBe('Raw physical power.');
	});

	it('accents the panel border with the attribute hue for a core attribute', () => {
		staticData.attributes = [STRENGTH];
		const { container } = render(AttributeTooltip, { props: { attributeId: EAttribute.Strength } });
		expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain(
			'var(--attr-strength)'
		);
	});

	it('uses a neutral accent for a non-core attribute', () => {
		staticData.attributes = [DEFENSE];
		const { container } = render(AttributeTooltip, { props: { attributeId: EAttribute.Defense } });
		expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain(
			'var(--text-secondary)'
		);
	});

	it('shows the effect direction/magnitude/duration in the chip context (a buff)', () => {
		staticData.attributes = [STRENGTH];
		const { container, getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Strength,
				effect: { modifierType: EModifierType.Additive, amount: 5, durationMs: 1000 }
			}
		});

		const effect = getByTestId('attr-tip-effect');
		// Raising a beneficial attribute is a buff; the magnitude is signed and the duration in seconds.
		expect(effect.textContent).toContain('+5');
		expect(effect.textContent).toContain('Buff');
		expect(effect.textContent).toContain('1s');
		expect(container.querySelector('.at-effect-mag')?.getAttribute('style')).toContain('var(--effect-buff)');
	});

	it('classifies a lowered beneficial attribute as a debuff', () => {
		staticData.attributes = [DEFENSE];
		const { getByTestId, container } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Defense,
				effect: { modifierType: EModifierType.Additive, amount: -5, durationMs: 2000 }
			}
		});
		const effect = getByTestId('attr-tip-effect');
		expect(effect.textContent).toContain('-5');
		expect(effect.textContent).toContain('Debuff');
		expect(container.querySelector('.at-effect-mag')?.getAttribute('style')).toContain('var(--effect-debuff)');
	});

	it('treats raising a harmful attribute as a debuff for the chip it lands on', () => {
		staticData.attributes = [DOT];
		const { getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.DamageTakenPerSecond,
				effect: { modifierType: EModifierType.Additive, amount: 3, durationMs: 5000 }
			}
		});
		// DamageTakenPerSecond is harmful, so a positive amount is a debuff despite raising the value.
		expect(getByTestId('attr-tip-effect').textContent).toContain('Debuff');
	});

	it('degrades gracefully when the reference data is unavailable', () => {
		staticData.attributes = undefined;
		const { container } = render(AttributeTooltip, { props: { attributeId: EAttribute.Strength } });
		// Name falls back to the humanised enum name; the type label and description drop out.
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Strength');
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('');
		expect(container.querySelector('.at-desc')).toBeNull();
	});
});
