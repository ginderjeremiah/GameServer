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
const TOUGHNESS = makeAttribute(EAttribute.Toughness, 'Toughness', {
	attributeType: EAttributeType.Secondary,
	description:
		'Reduces all incoming direct damage by a percentage that grows with diminishing returns, never reaching full immunity.'
});
const DOT = makeAttribute(EAttribute.BleedDamagePerSecond, 'Bleed Damage Per Second', {
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
		staticData.attributes = [TOUGHNESS];
		const { container } = render(AttributeTooltip, { props: { attributeId: EAttribute.Toughness } });
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
					count: 1,
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

	it('breaks the stack down by source and shows the combined total in the chip context', () => {
		staticData.attributes = [STRENGTH];
		const { getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Strength,
				effect: {
					modifierType: EModifierType.Additive,
					amount: 20, // combined total of the three applications (two Battle Cry + one War Drum)
					count: 3,
					durationMs: 1000,
					remainingMs: 1000,
					sources: [
						{ amount: 10, sourceName: 'Battle Cry', count: 2 },
						{ amount: 10, sourceName: 'War Drum', count: 1 }
					]
				}
			}
		});

		// Headline shows the combined total and the count label every application; the breakdown rolls the
		// applications up to one row per contributing source (the stack shares one expiry, shown by the pill).
		expect(getByTestId('attr-tip-effect').textContent).toContain('+20');
		const stacks = getByTestId('attr-tip-stacks');
		expect(stacks.textContent).toContain('3 applications');
		const rows = stacks.querySelectorAll('.at-effect-stack-row');
		expect(rows).toHaveLength(2);
		expect(rows[0].textContent).toContain('+10');
		expect(rows[0].textContent).toContain('Battle Cry');
		expect(rows[1].textContent).toContain('+10');
		expect(rows[1].textContent).toContain('War Drum');
	});

	it('classifies a lowered beneficial attribute as a debuff and depletes the pill', () => {
		staticData.attributes = [TOUGHNESS];
		const { getByTestId, container } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Toughness,
				effect: { modifierType: EModifierType.Additive, amount: -5, count: 1, durationMs: 2000, remainingMs: 1000 }
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
				attributeId: EAttribute.BleedDamagePerSecond,
				effect: { modifierType: EModifierType.Additive, amount: 3, count: 1, durationMs: 5000, remainingMs: 5000 }
			}
		});
		// BleedDamagePerSecond is harmful, so a positive amount is a debuff despite raising the value.
		expect(getByTestId('attr-tip-effect').textContent).toContain('Debuff');
	});

	it('omits the countdown pill when no remaining time is supplied', () => {
		staticData.attributes = [STRENGTH];
		const { container, getByTestId } = render(AttributeTooltip, {
			props: {
				attributeId: EAttribute.Strength,
				effect: { modifierType: EModifierType.Additive, amount: 5, count: 1, durationMs: 1000 }
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
