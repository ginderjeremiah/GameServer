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
import type { Battler } from '$lib/battle';

let owner: Battler;
let opponent: Battler;

beforeEach(() => {
	owner = makeBattler({ name: 'Aelara', attributes: [{ attributeId: EAttribute.Strength, amount: 20 }] });
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
		staticData.attributes[EAttribute.Strength] = { id: EAttribute.Strength, name: 'Strength', description: '' };
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
		const fast = makeBattler({ attributes: [{ attributeId: EAttribute.Strength, amount: 20 }] });
		fast.cdMultiplier = 2;
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
