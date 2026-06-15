import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import {
	EAttribute,
	EModifierType,
	ESkillEffectTarget,
	type IAttribute,
	type ISkill,
	type ISkillEffect
} from '$lib/api';
import { statify } from '$lib/common';

// The chips resolve attribute names and the effect's source skill through the reference-data store.
const { staticData } = vi.hoisted(() => ({
	staticData: { attributes: [] as IAttribute[], skills: [] as (ISkill | undefined)[] }
}));
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
	// A skill owning the effect id the fixtures use, so the tooltip can resolve the source.
	staticData.skills = [{ id: 0, name: 'Battle Cry', effects: [effect({ id: 1 })] } as ISkill];
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

		// It opens for the chip's attribute and carries the effect's magnitude/direction.
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Strength');
		const effectRow = container.querySelector('[data-testid="attr-tip-effect"]') as HTMLElement;
		expect(effectRow.textContent).toContain('+5');
		expect(effectRow.textContent).toContain('Buff');
		// The live effect drives a countdown pill (the chip just applied, so it's at full duration).
		expect((container.querySelector('.tt-duration-text') as HTMLElement).textContent?.trim()).toBe('1.0s');
		// The source skill is resolved from the reference data.
		expect((container.querySelector('.at-effect-source-name') as HTMLElement).textContent).toBe('Battle Cry');

		// Moving over the chip keeps it open (the controller repositions the panel).
		await fireEvent.mouseMove(container.querySelector('.effect-chip') as HTMLElement);
		expect(container.querySelector('.tt-title-name')).not.toBeNull();

		await fireEvent.mouseLeave(container.querySelector('.effect-chip') as HTMLElement);
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});

	it('closes the tooltip when the hovered effect expires under the cursor', async () => {
		// A reactive battler so the chip's removal propagates (the live app statifies its battlers).
		const live = statify(makeBattler());
		live.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, durationMs: 1000 }));
		const { container } = render(ActiveEffectChips, { props: { battler: live } });

		await fireEvent.mouseEnter(container.querySelector('.effect-chip') as HTMLElement);
		expect(container.querySelector('.tt-title-name')).not.toBeNull();

		// Expire the effect: its chip is spliced out with no mouseleave, which previously left the
		// tooltip stuck open. The panel must now close itself.
		live.advanceEffects(1000);
		flushSync();
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});
});
