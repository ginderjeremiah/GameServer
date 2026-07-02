import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EDamageType, ERarity, EAttribute, ESkillAcquisition, type IAttribute, type ISkill } from '$lib/api';

// The breakdown resolves the crit-chance label and attribute names through the reference-data store,
// so it is mocked. The object backs the hoisted `vi.mock` factory, but its `attributes` are populated
// after the imports run — the `EAttribute` enum isn't initialized while `vi.hoisted` executes.
const { staticData } = vi.hoisted(() => ({ staticData: { attributes: [] as unknown[] } }));

vi.mock('$stores', () => ({ staticData }));

import SkillDamageBreakdown from '$routes/game/screens/skills/SkillDamageBreakdown.svelte';
import type { SkillMetrics, SkillsView } from '$routes/game/screens/skills/skills-view.svelte';

// CriticalChanceMultiplier is flagged `isPercentage`, reused for the per-skill crit chance's display
// formatting, so the chance renders as e.g. `50%`.
staticData.attributes = [
	{ id: EAttribute.CriticalChanceMultiplier, name: 'Critical Chance', code: 'CRIT', isPercentage: true, decimals: 0 }
] as unknown as IAttribute[];

const stubSkill = (over: Partial<ISkill> = {}): ISkill => ({
	id: 0,
	name: 'Cleave',
	baseDamage: 50,
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	designerNotes: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	acquisition: ESkillAcquisition.Player,
	...over
});

// The per-skill crit chance now rides SkillMetrics (#1453) rather than a page-wide SkillsView value.
const stubMetrics = (over: Partial<SkillMetrics> = {}): SkillMetrics => ({
	skill: stubSkill(),
	rawDamage: 50,
	cooldown: 1,
	contributions: [],
	critChance: 0.5,
	critMultiplier: 1.5,
	...over
});

// Only the members the component reads are stubbed; the per-skill reads ignore the id (one skill here).
function stubView(over: Partial<Record<string, unknown>> = {}): SkillsView {
	return {
		critDamage: 2,
		critBonus: () => 25,
		mitigatedAmount: () => 5,
		effective: () => 70,
		...over
	} as unknown as SkillsView;
}

afterEach(cleanup);

describe('SkillDamageBreakdown — critical row', () => {
	it('renders a Critical row with chance, ×multiplier and the expected bonus', () => {
		const { container } = render(SkillDamageBreakdown, { props: { view: stubView(), metrics: stubMetrics() } });
		const crit = container.querySelector('.brk-line.crit') as HTMLElement;
		expect(crit).not.toBeNull();
		expect(crit.textContent).toContain('Critical');
		expect(crit.textContent).toContain('50%'); // this skill's own crit chance (isPercentage)
		expect(crit.textContent).toContain('×2'); // crit damage multiplier
		expect(crit.textContent).toContain('+25'); // expected crit bonus
	});

	it('omits the Critical row when the skill has no crit chance', () => {
		const { container } = render(SkillDamageBreakdown, {
			props: { view: stubView({ critBonus: () => 0 }), metrics: stubMetrics({ critChance: 0, critMultiplier: 1 }) }
		});
		expect(container.querySelector('.brk-line.crit')).toBeNull();
		// The Toughness-mitigation and effective-hit lines still render.
		expect(container.querySelector('.brk-line.def .brk-v')?.textContent).toContain('5');
		expect(container.querySelector('.brk-line.total .brk-v')?.textContent).toContain('70');
	});

	it('orders the crit row between the scaling rows and the Toughness-mitigation line', () => {
		const { container } = render(SkillDamageBreakdown, { props: { view: stubView(), metrics: stubMetrics() } });
		// Drop Svelte's scoped style class (e.g. `svelte-xxxx`) so only the semantic classes are compared.
		const rows = Array.from(container.querySelectorAll('.brk-line')).map((el) =>
			Array.from(el.classList)
				.filter((c) => !c.startsWith('svelte-'))
				.join(' ')
		);
		expect(rows).toEqual(['brk-line crit', 'brk-line def', 'brk-line total']);
	});
});
