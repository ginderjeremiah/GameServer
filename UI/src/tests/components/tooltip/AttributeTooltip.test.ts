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

	it('shows the effect magnitude/direction, source skill and a depleting pill in the chip context (a buff)', () => {
		staticData.attributes = [STRENGTH];
		const { container, getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Strength,
				effect: {
					modifierType: EModifierType.Additive,
					amount: 5,
					durationMs: 1000,
					remainingMs: 1000,
					sourceName: 'Battle Cry'
				}
			}
		});

		// Raising a beneficial attribute is a buff; the magnitude is signed and tinted by direction.
		const effect = getByTestId('attr-tip-effect');
		expect(effect.textContent).toContain('+5');
		expect(effect.textContent).toContain('Buff');
		expect(container.querySelector('.at-effect-mag')?.getAttribute('style')).toContain('var(--effect-buff)');
		// A full (remaining == duration) countdown pill shows the remaining time.
		expect((container.querySelector('.tt-duration-text') as HTMLElement).textContent?.trim()).toBe('1.0s');
		expect((container.querySelector('.tt-duration-fill') as HTMLElement).style.width).toBe('100%');
		// The source skill that applied the effect is named.
		expect((container.querySelector('.at-effect-source-name') as HTMLElement).textContent).toBe('Battle Cry');
	});

	it('breaks down stacked applications and shows the combined total in the chip context', () => {
		staticData.attributes = [STRENGTH];
		const { getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Strength,
				effect: {
					modifierType: EModifierType.Additive,
					amount: 15, // combined total of three +5 applications
					stackAmount: 5,
					durationMs: 1000,
					remainingMs: 1000,
					sourceName: 'Battle Cry',
					applications: [
						{ remainingMs: 1000, durationMs: 1000 },
						{ remainingMs: 600, durationMs: 1000 },
						{ remainingMs: 200, durationMs: 1000 }
					]
				}
			}
		});

		// Headline shows the combined total; the breakdown lists each application's own amount + remaining.
		expect(getByTestId('attr-tip-effect').textContent).toContain('+15');
		const stacks = getByTestId('attr-tip-stacks');
		expect(stacks.textContent).toContain('3 applications');
		const rows = stacks.querySelectorAll('.at-effect-stack-row');
		expect(rows).toHaveLength(3);
		expect(rows[0].textContent).toContain('+5');
		expect(rows[0].textContent).toContain('1.0s');
		expect(rows[2].textContent).toContain('0.2s');
	});

	it('classifies a lowered beneficial attribute as a debuff and depletes the pill', () => {
		staticData.attributes = [DEFENSE];
		const { getByTestId, container } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Defense,
				effect: { modifierType: EModifierType.Additive, amount: -5, durationMs: 2000, remainingMs: 1000 }
			}
		});
		const effect = getByTestId('attr-tip-effect');
		expect(effect.textContent).toContain('-5');
		expect(effect.textContent).toContain('Debuff');
		expect(container.querySelector('.at-effect-mag')?.getAttribute('style')).toContain('var(--effect-debuff)');
		// Half elapsed → the pill is half depleted.
		expect((container.querySelector('.tt-duration-fill') as HTMLElement).style.width).toBe('50%');
	});

	it('treats raising a harmful attribute as a debuff for the chip it lands on', () => {
		staticData.attributes = [DOT];
		const { getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.DamageTakenPerSecond,
				effect: { modifierType: EModifierType.Additive, amount: 3, durationMs: 5000, remainingMs: 5000 }
			}
		});
		// DamageTakenPerSecond is harmful, so a positive amount is a debuff despite raising the value.
		expect(getByTestId('attr-tip-effect').textContent).toContain('Debuff');
	});

	it('omits the countdown pill when no remaining time is supplied', () => {
		staticData.attributes = [STRENGTH];
		const { container, getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Strength,
				effect: { modifierType: EModifierType.Additive, amount: 5, durationMs: 1000 }
			}
		});
		// The effect summary still renders, but with no live timer there is no pill, and with no
		// resolvable source there is no source row.
		expect(getByTestId('attr-tip-effect').textContent).toContain('+5');
		expect(container.querySelector('.tt-duration-pill')).toBeNull();
		expect(container.querySelector('.at-effect-source')).toBeNull();
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
