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
	scalingAttributeId: EAttribute.Strength,
	scalingAmount: 0,
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

	it('renders one icon tile per active effect, tinted by buff/debuff direction', () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		battler.applyEffect(
			effect({ id: 2, target: ESkillEffectTarget.Opponent, attributeId: EAttribute.Defense, amount: -5 })
		);
		const { container } = render(ActiveEffectChips, { props: { battler } });

		const chips = container.querySelectorAll('.effect-chip');
		expect(chips).toHaveLength(2);
		// A raised Strength is a buff; a lowered Defense is a debuff.
		expect((chips[0] as HTMLElement).style.getPropertyValue('--chip-accent')).toBe('var(--effect-buff)');
		expect(chips[0].querySelector('.chip-mag')?.textContent).toContain('+5');
		expect((chips[1] as HTMLElement).style.getPropertyValue('--chip-accent')).toBe('var(--effect-debuff)');
		expect(chips[1].querySelector('.chip-mag')?.textContent).toContain('-5');
		// The attribute name moves to the tooltip/accessible name rather than crowding the tile.
		expect(chips[0].getAttribute('aria-label')).toContain('Strength');
		expect(chips[1].getAttribute('aria-label')).toContain('Defense');
	});

	it('groups stacked applications of one effect into a single chip with a count badge and combined total', () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		const { container, getByTestId } = render(ActiveEffectChips, { props: { battler } });

		// One chip for the three stacked applications, its magnitude the combined total (+15).
		const chips = container.querySelectorAll('.effect-chip');
		expect(chips).toHaveLength(1);
		expect(chips[0].querySelector('.chip-mag')?.textContent).toContain('+15');
		// A count badge in the corner shows the number of active applications.
		expect(getByTestId('chip-count').textContent).toBe('3');
		expect(chips[0].getAttribute('aria-label')).toContain('3 applications');
	});

	it('collapses different source skills on the same attribute+type into one chip, summing actual amounts', async () => {
		// Two DIFFERENT effects on the same (Strength, additive) from two DIFFERENT skills, with DIFFERENT
		// amounts — the case the removed `combineEffectAmount(amount × count)` shortcut couldn't total correctly.
		staticData.skills = [
			{ id: 0, name: 'Battle Cry', effects: [effect({ id: 1 })] } as ISkill,
			{ id: 1, name: 'War Drum', effects: [effect({ id: 2, amount: 3 })] } as ISkill
		];
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		battler.applyEffect(effect({ id: 2, attributeId: EAttribute.Strength, amount: 3 }));
		const { container, getByTestId } = render(ActiveEffectChips, { props: { battler } });

		// One chip for the two applications; the magnitude is the summed total (+8), not amount × count (+10).
		const chips = container.querySelectorAll('.effect-chip');
		expect(chips).toHaveLength(1);
		expect(chips[0].querySelector('.chip-mag')?.textContent).toContain('+8');
		expect(getByTestId('chip-count').textContent).toBe('2');

		// The tooltip breaks the stack down by each application's own amount and source skill.
		await fireEvent.mouseEnter(chips[0] as HTMLElement);
		const rows = getByTestId('attr-tip-stacks').querySelectorAll('.at-effect-stack-row');
		expect(rows).toHaveLength(2);
		expect(rows[0].textContent).toContain('+5');
		expect(rows[0].textContent).toContain('Battle Cry');
		expect(rows[1].textContent).toContain('+3');
		expect(rows[1].textContent).toContain('War Drum');
	});

	it('shows no count badge for a single (unstacked) application', () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		const { container, queryByTestId } = render(ActiveEffectChips, { props: { battler } });

		expect(container.querySelectorAll('.effect-chip')).toHaveLength(1);
		expect(queryByTestId('chip-count')).toBeNull();
	});

	it('breaks the stack down by source in the tooltip alongside the total', async () => {
		// Two applications from the SAME source fold into one breakdown row (aggregated by source) while the
		// count label still reports both applications.
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5, durationMs: 1000 }));
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5, durationMs: 1000 }));
		const { container, getByTestId } = render(ActiveEffectChips, { props: { battler } });

		await fireEvent.mouseEnter(container.querySelector('.effect-chip') as HTMLElement);

		// The headline magnitude is the combined total, and the breakdown rolls the repeats up to one source.
		expect(getByTestId('attr-tip-effect').textContent).toContain('+10');
		const stacks = getByTestId('attr-tip-stacks');
		expect(stacks.textContent).toContain('2 applications');
		const rows = stacks.querySelectorAll('.at-effect-stack-row');
		expect(rows).toHaveLength(1);
		expect(rows[0].textContent).toContain('+10');
		expect(rows[0].textContent).toContain('Battle Cry');
	});

	it('sweeps the radial overlay from the render-interpolated remaining duration', () => {
		battler.applyEffect(effect({ id: 1, durationMs: 1000 }));
		battler.activeEffects[0].renderRemainingMs = 500; // half remaining
		const { container } = render(ActiveEffectChips, { props: { battler } });

		// Half remaining -> the revealed arc is 180 of 360 degrees.
		expect(container.querySelector('.cooldown-overlay')?.getAttribute('style')).toContain('180deg');
	});

	it('fully reveals the icon (360deg sweep) for a freshly applied effect', () => {
		battler.applyEffect(effect({ id: 1, durationMs: 2000 }));
		battler.activeEffects[0].renderRemainingMs = 2000; // just applied / refreshed
		const { container } = render(ActiveEffectChips, { props: { battler } });

		expect(container.querySelector('.cooldown-overlay')?.getAttribute('style')).toContain('360deg');
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

	it('makes each chip a focusable button that opens the tooltip on focus and hides it on blur', async () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5, durationMs: 1000 }));
		const { container } = render(ActiveEffectChips, { props: { battler } });

		const chip = container.querySelector('.effect-chip') as HTMLElement;
		// A real <button> so the tooltip is keyboard/screen-reader reachable, mirroring the skill slots.
		expect(chip.tagName).toBe('BUTTON');
		expect(container.querySelector('.tt-title-name')).toBeNull();

		await fireEvent.focus(chip);
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Strength');

		await fireEvent.blur(chip);
		expect(container.querySelector('.tt-title-name')).toBeNull();
	});

	it('associates each chip with the shared attribute tooltip via aria-describedby', () => {
		battler.applyEffect(effect({ id: 1, attributeId: EAttribute.Strength, amount: 5 }));
		battler.applyEffect(effect({ id: 2, attributeId: EAttribute.Defense, amount: -5 }));
		const { container } = render(ActiveEffectChips, { props: { battler } });

		const ids = [...container.querySelectorAll('.effect-chip')].map((c) => c.getAttribute('aria-describedby'));
		// Each chip points at the row's single shared panel, so its effect detail is announced on focus.
		expect(ids.every((id) => id != null && /^tooltip-\d+$/.test(id))).toBe(true);
		expect(new Set(ids).size).toBe(1);
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
