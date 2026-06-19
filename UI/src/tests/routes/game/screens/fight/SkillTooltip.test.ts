import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, type IAttribute } from '$lib/api';

// The tooltip resolves the opponent through the battle engine and attribute names through the
// reference-data store; both are mocked so the damage maths is fully controlled by the test.
const { mockBattleEngine, staticData } = vi.hoisted(() => ({
	mockBattleEngine: { getOpponent: vi.fn() },
	staticData: { attributes: [] as IAttribute[] }
}));

vi.mock('$lib/engine', () => ({ battleEngine: mockBattleEngine }));
vi.mock('$stores', () => ({ staticData }));

import SkillTooltip from '$routes/game/screens/fight/SkillTooltip.svelte';
import { makeBattler, makeSkill } from './fight-fixtures';
import { makeAttribute } from '../../../../fixtures/attributes';
import type { Battler } from '$lib/battle';

let owner: Battler;
let opponent: Battler;

beforeEach(() => {
	// These raw fixtures skip the derived pass, so CooldownRecovery is supplied explicitly: 1 is the
	// base-1 multiplier every real battler carries (cdMultiplier reads the attribute directly now).
	owner = makeBattler({
		name: 'Aelara',
		attributes: [
			{ attributeId: EAttribute.Strength, amount: 20 },
			{ attributeId: EAttribute.CooldownRecovery, amount: 1 }
		]
	});
	opponent = makeBattler({ name: 'Dire Wolf', attributes: [{ attributeId: EAttribute.Defense, amount: 5 }] });
	mockBattleEngine.getOpponent.mockReturnValue(opponent);
	staticData.attributes = [];
});

afterEach(cleanup);

describe('SkillTooltip', () => {
	it('renders only the hidden anchor when no skill is provided', () => {
		const { container, queryByText } = render(SkillTooltip, { props: { skill: undefined } });
		expect(queryByText('Damage breakdown')).toBeNull();
		// The shell stays mounted (hidden) so the tooltip system can still anchor to it.
		const panel = container.querySelector('.tt-shell') as HTMLElement;
		expect(panel.style.display).toBe('none');
	});

	it('composes the tooltip shell with an accent-tinted glow', () => {
		const skill = makeSkill(owner, { name: 'Cleave', cooldownMs: 1000 });
		const { container } = render(SkillTooltip, { props: { skill } });
		const shell = container.querySelector('.tt-shell') as HTMLElement;
		expect(shell).not.toBeNull();
		// Migrated to TooltipShell: accent left border + opt-in accent-tinted glow.
		expect(shell.classList.contains('glow')).toBe(true);
		expect(shell.getAttribute('style')).toContain('border-left: 3px solid var(--accent)');
	});

	it('shows the damage breakdown: base, per-attribute multiplier, enemy defense and total', () => {
		staticData.attributes = [makeAttribute(EAttribute.Strength, 'Strength')];
		const skill = makeSkill(owner, {
			name: 'Cleave',
			baseDamage: 10,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2 }],
			cooldownMs: 1000
		});
		const { container, getByText } = render(SkillTooltip, { props: { skill } });

		expect(getByText('Cleave')).toBeTruthy();
		const list = container.querySelector('.tt-dmg-list') as HTMLElement;
		// base 10, STR(20) x2 = +40, enemy defense -5  ->  total 10 + 40 - 5 = 45
		expect(list.textContent).toContain('Strength');
		expect(list.textContent).toContain('10');
		expect(list.textContent).toContain('+40');
		expect(list.textContent).toContain('-5');
		expect((container.querySelector('.tt-total-value') as HTMLElement).textContent).toContain('45');
	});

	it('omits the enemy-defense row and uses raw damage when there is no opponent', () => {
		mockBattleEngine.getOpponent.mockReturnValue(undefined);
		const skill = makeSkill(owner, {
			baseDamage: 10,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2 }]
		});
		const { container } = render(SkillTooltip, { props: { skill } });
		expect(container.textContent).not.toContain('Enemy defense');
		// total = base 10 + STR(20) x2 (40), with no defense subtracted = 50
		expect((container.querySelector('.tt-total-value') as HTMLElement).textContent).toContain('50');
	});

	it('falls back to the enum attribute name when reference data is missing', () => {
		// staticData.attributes is left empty, so attributeName falls back to EAttribute[id].
		const skill = makeSkill(owner, { damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }] });
		const { container } = render(SkillTooltip, { props: { skill } });
		expect((container.querySelector('.tt-dmg-list') as HTMLElement).textContent).toContain('Strength');
	});

	it('derives cooldown and DPS, shortening the cooldown for a faster battler', () => {
		// cdMultiplier is the CooldownRecovery attribute read directly — a base-1 multiplier, so 2 = double
		// speed (fixtures skip the derived pass, so CooldownRecovery is taken verbatim from the supplied attributes).
		const fast = makeBattler({
			attributes: [
				{ attributeId: EAttribute.Strength, amount: 20 },
				{ attributeId: EAttribute.CooldownRecovery, amount: 2 }
			]
		});
		const skill = makeSkill(fast, {
			baseDamage: 10,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2 }],
			cooldownMs: 1000
		});
		const { container } = render(SkillTooltip, { props: { skill } });
		const metrics = container.querySelector('.tt-metrics') as HTMLElement;
		// adjustedCd = 1000ms / 1000 / cdMult(2) = 0.5s; total 45; DPS = 45 / 0.5 = 90
		expect(metrics.textContent).toContain('0.5s');
		expect(metrics.textContent).toContain('90');
	});

	it('renders an "On hit" effect line per authored effect, tinted by buff/debuff direction', async () => {
		const { EModifierType, ESkillEffectTarget } = await import('$lib/api');
		staticData.attributes = [
			makeAttribute(EAttribute.Strength, 'Strength'),
			makeAttribute(EAttribute.Defense, 'Defense')
		];
		const skill = makeSkill(owner, {
			name: 'Warcry',
			effects: [
				{
					id: 1,
					target: ESkillEffectTarget.Self,
					attributeId: EAttribute.Strength,
					modifierTypeId: EModifierType.Additive,
					amount: 15,
					durationMs: 5000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				},
				{
					id: 2,
					target: ESkillEffectTarget.Opponent,
					attributeId: EAttribute.Defense,
					modifierTypeId: EModifierType.Additive,
					amount: -10,
					durationMs: 3000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				}
			]
		});
		const { container, getByText } = render(SkillTooltip, { props: { skill } });

		expect(getByText('On hit')).toBeTruthy();
		const lines = container.querySelectorAll('.effect-line');
		expect(lines).toHaveLength(2);
		expect(lines[0].textContent).toContain('+15');
		expect(lines[0].textContent).toContain('Strength');
		expect(lines[0].textContent).toContain('self · 5s');
		expect((lines[0].querySelector('.mag') as HTMLElement).style.color).toBe('var(--effect-buff)');
		expect(lines[1].textContent).toContain('-10');
		expect((lines[1].querySelector('.mag') as HTMLElement).style.color).toBe('var(--effect-debuff)');
	});

	it('resolves a scaling effect line against the caster (owner) attributes', async () => {
		const { EModifierType, ESkillEffectTarget } = await import('$lib/api');
		staticData.attributes = [makeAttribute(EAttribute.Strength, 'Strength')];
		// owner Strength is 20; a base 10 effect scaling at 0.5/STR resolves to 10 + 20×0.5 = 20.
		const skill = makeSkill(owner, {
			name: 'Empower',
			effects: [
				{
					id: 1,
					target: ESkillEffectTarget.Self,
					attributeId: EAttribute.Strength,
					modifierTypeId: EModifierType.Additive,
					amount: 10,
					durationMs: 5000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0.5
				}
			]
		});
		const { container } = render(SkillTooltip, { props: { skill } });
		const line = container.querySelector('.effect-line') as HTMLElement;
		expect(line.textContent).toContain('+20');
	});

	it('omits the "On hit" section for a skill with no effects', () => {
		const skill = makeSkill(owner, { effects: [] });
		const { queryByText } = render(SkillTooltip, { props: { skill } });
		expect(queryByText('On hit')).toBeNull();
	});

	it('reads READY for a fully-charged skill and a remaining time for one on cooldown', () => {
		const ready = makeSkill(owner, { cooldownMs: 1000 });
		ready.renderChargeTime = 1000; // fully charged
		const readyRender = render(SkillTooltip, { props: { skill: ready } });
		expect(readyRender.getByText('READY')).toBeTruthy();
		cleanup();

		const cooling = makeSkill(owner, { cooldownMs: 1000 });
		cooling.renderChargeTime = 0; // just used
		const coolingRender = render(SkillTooltip, { props: { skill: cooling } });
		expect(coolingRender.queryByText('READY')).toBeNull();
		expect((coolingRender.container.querySelector('.tt-cooldown-text') as HTMLElement).textContent).toContain('s');
	});
});
