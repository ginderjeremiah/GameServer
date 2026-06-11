import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EModifierType, ESkillEffectTarget, type IAttribute, type ISkillEffect } from '$lib/api';

// The chips resolve attribute names through the reference-data store.
const { staticData } = vi.hoisted(() => ({ staticData: { attributes: [] as IAttribute[] } }));
vi.mock('$stores', () => ({ staticData }));

import ActiveEffectChips from '$routes/game/screens/fight/ActiveEffectChips.svelte';
import { makeBattler } from './fight-fixtures';
import type { Battler } from '$lib/battle';

const effect = (over: Partial<ISkillEffect> = {}): ISkillEffect => ({
	id: 1,
	target: ESkillEffectTarget.Self,
	attributeId: EAttribute.Strength,
	modifierTypeId: EModifierType.Additive,
	amount: 5,
	durationMs: 1000,
	...over
});

let battler: Battler;

beforeEach(() => {
	staticData.attributes = [
		{ id: EAttribute.Strength, name: 'Strength', description: '' },
		{ id: EAttribute.Defense, name: 'Defense', description: '' }
	];
	battler = makeBattler();
});

afterEach(cleanup);

describe('ActiveEffectChips', () => {
	it('renders nothing when the battler has no active effects', () => {
		const { queryByTestId } = render(ActiveEffectChips, { props: { battler } });
		expect(queryByTestId('effect-chips')).toBeNull();
	});

	it('renders one chip per active effect, tinted by buff/debuff direction', () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		battler.applyEffect(
			effect({ id: 2, target: ESkillEffectTarget.Opponent, attributeId: EAttribute.Defense, amount: -5 })
		);
		const { container } = render(ActiveEffectChips, { props: { battler } });

		const chips = container.querySelectorAll('.effect-chip');
		expect(chips).toHaveLength(2);
		// A raised Strength is a buff; a lowered Defense is a debuff.
		expect((chips[0] as HTMLElement).style.getPropertyValue('--chip-accent')).toBe('var(--effect-buff)');
		expect(chips[0].textContent).toContain('+5');
		expect(chips[0].textContent).toContain('Strength');
		expect((chips[1] as HTMLElement).style.getPropertyValue('--chip-accent')).toBe('var(--effect-debuff)');
		expect(chips[1].textContent).toContain('-5');
		expect(chips[1].textContent).toContain('Defense');
	});

	it('sizes the countdown fill from the render-interpolated remaining duration', () => {
		battler.applyEffect(effect({ id: 1, durationMs: 1000 }));
		battler.activeEffects[0].renderRemainingMs = 500; // half elapsed
		const { container } = render(ActiveEffectChips, { props: { battler } });

		const fill = container.querySelector('.chip-fill') as HTMLElement;
		expect(fill.style.width).toBe('50%');
		expect((container.querySelector('.chip-time') as HTMLElement).textContent).toContain('0.5s');
	});

	it('right-aligns the row when reversed (enemy/boss layout)', () => {
		battler.applyEffect(effect());
		const { getByTestId } = render(ActiveEffectChips, { props: { battler, reversed: true } });
		expect(getByTestId('effect-chips').classList.contains('reversed')).toBe(true);
	});
});
