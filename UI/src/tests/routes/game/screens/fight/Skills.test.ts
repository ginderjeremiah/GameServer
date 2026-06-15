import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EAttribute, EModifierType, ESkillEffectTarget, type IAttribute } from '$lib/api';

// Skills renders a SkillTooltip child, which resolves the opponent through the battle engine
// and attribute names through the reference-data store; both are mocked.
const { mockBattleEngine, staticData } = vi.hoisted(() => ({
	mockBattleEngine: { getOpponent: vi.fn() },
	staticData: { attributes: [] as IAttribute[] }
}));

vi.mock('$lib/engine', () => ({ battleEngine: mockBattleEngine }));
vi.mock('$stores', () => ({ staticData }));

import Skills from '$routes/game/screens/fight/Skills.svelte';
import { makeBattler, makeSkill } from './fight-fixtures';
import type { Battler } from '$lib/battle';

let battler: Battler;

beforeEach(() => {
	battler = makeBattler();
	// Consulted by the SkillTooltip child once a slot is hovered.
	mockBattleEngine.getOpponent.mockReturnValue(makeBattler({ name: 'Dire Wolf' }));
});

afterEach(cleanup);

describe('Skills', () => {
	it('renders a slot with icon and label for each defined skill', () => {
		battler.skills = [
			makeSkill(battler, { name: 'Slash', iconPath: '/slash.png' }),
			makeSkill(battler, { id: 2, name: 'Cleave', iconPath: '/cleave.png' })
		];
		const { container, getByAltText, getByText } = render(Skills, { props: { battler, side: 'player' } });

		expect(container.querySelectorAll('.skill-slot')).toHaveLength(2);
		expect(getByAltText('Slash')).toBeTruthy();
		expect(getByAltText('Cleave')).toBeTruthy();
		expect(getByText('Slash')).toBeTruthy();
		expect(getByText('Cleave')).toBeTruthy();
	});

	it('renders an empty column with no icon or label for an unfilled skill slot', () => {
		battler.skills = [makeSkill(battler, { name: 'Slash' }), undefined];
		const { container } = render(Skills, { props: { battler, side: 'player' } });

		// Two columns are laid out, but only the filled one carries an icon and label.
		expect(container.querySelectorAll('.skill-column')).toHaveLength(2);
		expect(container.querySelectorAll('.skill-icon')).toHaveLength(1);
		expect(container.querySelectorAll('.skill-label')).toHaveLength(1);
	});

	it('marks a fully-charged skill ready and leaves one mid-cooldown un-ready', () => {
		const ready = makeSkill(battler, { name: 'Ready', cooldownMs: 1000 });
		ready.renderChargeTime = 1000; // 100% charged
		const charging = makeSkill(battler, { id: 2, name: 'Charging', cooldownMs: 1000 });
		charging.renderChargeTime = 500; // 50% charged
		battler.skills = [ready, charging];

		const { container } = render(Skills, { props: { battler, side: 'player' } });
		const slots = container.querySelectorAll('.skill-slot');
		expect(slots[0].classList.contains('ready')).toBe(true);
		expect(slots[1].classList.contains('ready')).toBe(false);
		// The cooldown sweep tracks the charge percentage (50% -> 180 of 360 degrees).
		expect(slots[1].querySelector('.cooldown-overlay')?.getAttribute('style')).toContain('180deg');
	});

	it('shows the effect badge only on skills that carry effects', () => {
		battler.skills = [
			makeSkill(battler, {
				name: 'Curse',
				effects: [
					{
						id: 1,
						target: ESkillEffectTarget.Opponent,
						attributeId: EAttribute.Defense,
						modifierTypeId: EModifierType.Additive,
						amount: -5,
						durationMs: 3000
					}
				]
			}),
			makeSkill(battler, { id: 2, name: 'Slash', effects: [] })
		];
		const { container } = render(Skills, { props: { battler, side: 'player' } });

		const slots = container.querySelectorAll('.skill-slot');
		expect(slots[0].querySelector('.effect-badge-anchor')).not.toBeNull();
		expect(slots[1].querySelector('.effect-badge-anchor')).toBeNull();
	});

	it('right-aligns the enemy skill row', () => {
		battler.skills = [makeSkill(battler)];
		const { container } = render(Skills, { props: { battler, side: 'enemy' } });
		expect(container.querySelector('.skills-row')?.classList.contains('reversed')).toBe(true);
	});

	it('reveals the skill tooltip for the hovered slot and hides it on leave', async () => {
		battler.skills = [makeSkill(battler, { name: 'Slash' })];
		const { container, queryByText } = render(Skills, { props: { battler, side: 'player' } });

		// The tooltip body is absent until a slot is hovered.
		expect(queryByText('Damage breakdown')).toBeNull();

		const slot = container.querySelector('.skill-slot') as HTMLElement;
		await fireEvent.mouseEnter(slot);
		expect(queryByText('Damage breakdown')).not.toBeNull();

		await fireEvent.mouseLeave(slot);
		expect(queryByText('Damage breakdown')).toBeNull();
	});

	it('reveals the skill tooltip on keyboard focus and hides it on blur', async () => {
		battler.skills = [makeSkill(battler, { name: 'Slash' })];
		const { container, queryByText } = render(Skills, { props: { battler, side: 'player' } });

		expect(queryByText('Damage breakdown')).toBeNull();

		const slot = container.querySelector('.skill-slot') as HTMLElement;
		await fireEvent.focus(slot);
		expect(queryByText('Damage breakdown')).not.toBeNull();

		await fireEvent.blur(slot);
		expect(queryByText('Damage breakdown')).toBeNull();
	});

	it('makes filled slots focusable buttons and leaves empty slots inert', () => {
		battler.skills = [makeSkill(battler, { name: 'Slash' }), undefined];
		const { container } = render(Skills, { props: { battler, side: 'player' } });

		const slots = container.querySelectorAll('.skill-slot');
		expect(slots).toHaveLength(2);
		// The filled slot is a focusable <button>; the empty placeholder is an inert <div>.
		expect(slots[0].tagName).toBe('BUTTON');
		expect(slots[1].tagName).toBe('DIV');
		expect(slots[1].getAttribute('aria-hidden')).toBe('true');
		// The misleading grid semantics are gone — no grid roles remain.
		expect(container.querySelector('[role="grid"]')).toBeNull();
		expect(container.querySelector('[role="gridcell"]')).toBeNull();
	});
});
