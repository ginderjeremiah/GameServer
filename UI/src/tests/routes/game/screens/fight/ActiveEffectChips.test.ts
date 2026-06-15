import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EAttribute, EModifierType, ESkillEffectTarget, type IAttribute, type ISkillEffect } from '$lib/api';

// The chips resolve attribute names through the reference-data store.
const { staticData } = vi.hoisted(() => ({ staticData: { attributes: [] as IAttribute[] } }));
vi.mock('$stores', () => ({ staticData }));

import ActiveEffectChips from '$routes/game/screens/fight/ActiveEffectChips.svelte';
import { makeBattler } from './fight-fixtures';
import { makeAttribute } from '../../../../fixtures/attributes';
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
		makeAttribute(EAttribute.Strength, 'Strength'),
		makeAttribute(EAttribute.Defense, 'Defense')
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
		expect((container.querySelector('.chip-time') as HTMLElement).textContent).toContain('0.50s');
	});

	it('formats integer seconds with two decimal places to prevent width jitter', () => {
		battler.applyEffect(effect({ id: 1, durationMs: 2000 }));
		battler.activeEffects[0].renderRemainingMs = 2000;
		const { container } = render(ActiveEffectChips, { props: { battler } });

		expect((container.querySelector('.chip-time') as HTMLElement).textContent).toContain('2.00s');
	});

	it('right-aligns the row when reversed (enemy/boss layout)', () => {
		battler.applyEffect(effect());
		const { getByTestId } = render(ActiveEffectChips, { props: { battler, reversed: true } });
		expect(getByTestId('effect-chips').classList.contains('reversed')).toBe(true);
	});

	it('opens the attribute tooltip with the effect detail when a chip is hovered', async () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5, durationMs: 1000 }));
		const { container } = render(ActiveEffectChips, { props: { battler } });

		// The tooltip stays hidden (no title) until a chip is hovered.
		expect(container.querySelector('.tt-title-name')).toBeNull();

		await fireEvent.mouseEnter(container.querySelector('.effect-chip') as HTMLElement);

		// It opens for the chip's attribute and carries the effect's magnitude/direction/duration.
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Strength');
		const effectRow = container.querySelector('[data-testid="attr-tip-effect"]') as HTMLElement;
		expect(effectRow.textContent).toContain('+5');
		expect(effectRow.textContent).toContain('Buff');

		// Moving over the chip keeps it open (the controller repositions the panel).
		await fireEvent.mouseMove(container.querySelector('.effect-chip') as HTMLElement);
		expect(container.querySelector('.tt-title-name')).not.toBeNull();

		await fireEvent.mouseLeave(container.querySelector('.effect-chip') as HTMLElement);
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});
});
