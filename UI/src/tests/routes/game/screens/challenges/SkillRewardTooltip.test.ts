import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EModifierType, ESkillAcquisition, ESkillEffectTarget, type ISkill } from '$lib/api';

// The tooltip resolves attribute display names from the reference-data store; a populated
// fixture lets us assert the real names render (rather than the enum-key fallback).
vi.mock('$stores', () => ({
	staticData: {
		attributes: [
			{ id: EAttribute.Intellect, name: 'Intellect' },
			{ id: EAttribute.Strength, name: 'Strength' }
		]
	}
}));

import SkillRewardTooltip from '$routes/game/screens/challenges/SkillRewardTooltip.svelte';

const makeSkill = (over: Partial<ISkill> = {}): ISkill => ({
	id: 1,
	name: 'Firebolt',
	baseDamage: 12,
	description: 'Hurls a bolt of fire.',
	damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1.5 }],
	effects: [],
	cooldownMs: 3000,
	iconPath: '',
	acquisition: ESkillAcquisition.Player,
	...over
});

afterEach(cleanup);

describe('SkillRewardTooltip', () => {
	it('renders the skill name under a "Skill" category label', () => {
		const { container } = render(SkillRewardTooltip, { props: { skill: makeSkill() } });
		expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('Firebolt');
		expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Skill');
	});

	it('shows base damage, the resolved scaling attribute and the cooldown in seconds', () => {
		const { container } = render(SkillRewardTooltip, { props: { skill: makeSkill() } });
		const text = container.textContent ?? '';
		expect(text).toContain('Base damage');
		expect(text).toContain('12');
		expect(text).toContain('Intellect');
		expect(text).toContain('×1.5');
		expect(text).toContain('3s');
	});

	it('renders an "On hit" line for each authored effect, omitting the section when there are none', () => {
		const withEffect = render(SkillRewardTooltip, {
			props: {
				skill: makeSkill({
					effects: [
						{
							id: 1,
							target: ESkillEffectTarget.Self,
							attributeId: EAttribute.Strength,
							modifierTypeId: EModifierType.Additive,
							amount: 5,
							durationMs: 5000,
							scalingAttributeId: EAttribute.Strength,
							scalingAmount: 0
						}
					]
				})
			}
		});
		expect(withEffect.container.textContent).toContain('On hit');
		expect(withEffect.container.textContent).toContain('Strength');
		cleanup();

		const noEffect = render(SkillRewardTooltip, { props: { skill: makeSkill({ effects: [] }) } });
		expect(noEffect.container.textContent).not.toContain('On hit');
	});

	it('omits the description section when the skill has none', () => {
		const { container } = render(SkillRewardTooltip, { props: { skill: makeSkill({ description: '' }) } });
		expect(container.textContent).not.toContain('Description');
	});

	describe('masked', () => {
		it('accents the panel with the neutral skill hue and masks the name', () => {
			const { container } = render(SkillRewardTooltip, { props: { skill: makeSkill(), masked: true } });
			expect((container.querySelector('.tt-shell') as HTMLElement).getAttribute('style')).toContain(
				'var(--accent-light)'
			);
			expect((container.querySelector('.tt-title-name') as HTMLElement).textContent).toBe('?????????');
			expect((container.querySelector('.tt-category-label') as HTMLElement).textContent).toBe('Skill');
			expect(container.textContent).not.toContain('Firebolt');
		});

		it('teases one masked row per scaling attribute and per effect', () => {
			const skill = makeSkill({
				damageMultipliers: [
					{ attributeId: EAttribute.Intellect, multiplier: 1.5 },
					{ attributeId: EAttribute.Strength, multiplier: 0.5 }
				],
				effects: [
					{
						id: 1,
						target: ESkillEffectTarget.Self,
						attributeId: EAttribute.Strength,
						modifierTypeId: EModifierType.Additive,
						amount: 5,
						durationMs: 5000,
						scalingAttributeId: EAttribute.Strength,
						scalingAmount: 0
					}
				]
			});
			const { container } = render(SkillRewardTooltip, { props: { skill, masked: true } });
			// Two damage multipliers + one effect → three masked rows.
			expect(container.querySelectorAll('.tt-qmark')).toHaveLength(3);
		});

		it('always shows a masked description teaser, even without scaling or effects', () => {
			const { container } = render(SkillRewardTooltip, {
				props: { skill: makeSkill({ damageMultipliers: [], effects: [], description: '' }), masked: true }
			});
			expect(container.querySelector('.tt-qmark')).toBeNull();
			expect(container.textContent).toContain('Description');
			expect(container.querySelector('.tt-masked-desc')).not.toBeNull();
		});
	});
});
