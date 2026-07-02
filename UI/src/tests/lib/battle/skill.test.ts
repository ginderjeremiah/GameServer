import { describe, it, expect } from 'vitest';
import { EDamageType, ERarity, EAttribute, EModifierType, ESkillAcquisition, ESkillEffectTarget } from '$lib/api';
import type { ISkill, IAttributeMultiplier } from '$lib/api';
import { BattleAttributes } from '$lib/battle/battle-attributes';
import { Skill } from '$lib/battle/skill';
import { Battler } from '$lib/battle';

const makeSkillData = (overrides: Partial<ISkill> = {}): ISkill => ({
	id: 1,
	name: 'Slash',
	baseDamage: 10,
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: 'A basic slash',
	cooldownMs: 1000,
	iconPath: '/icons/slash.png',
	rarityId: ERarity.Common,
	designerNotes: '',
	word: '',
	pronunciation: '',
	translation: '',
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	acquisition: ESkillAcquisition.Player,
	...overrides
});

const makeMockOwner = (attrs: [EAttribute, number][] = []) => {
	const attributes = new BattleAttributes(
		attrs.map(([attributeId, amount]) => ({ attributeId, amount })),
		false
	);
	return { attributes } as Battler;
};

describe('Skill', () => {
	describe('constructor', () => {
		it('copies all properties from skill data', () => {
			const effects = [
				{
					id: 1,
					target: ESkillEffectTarget.Self,
					attributeId: EAttribute.Strength,
					modifierTypeId: EModifierType.Additive,
					amount: 5,
					durationMs: 3000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				}
			];
			const data = makeSkillData({ id: 5, name: 'Fireball', baseDamage: 25, cooldownMs: 2000, effects });
			const owner = makeMockOwner();
			const skill = new Skill(data, owner);

			expect(skill.id).toBe(5);
			expect(skill.name).toBe('Fireball');
			expect(skill.baseDamage).toBe(25);
			expect(skill.cooldownMs).toBe(2000);
			expect(skill.effects).toEqual(effects);
		});

		it('initializes chargeTime and renderChargeTime to 0', () => {
			const skill = new Skill(makeSkillData(), makeMockOwner());

			expect(skill.chargeTime).toBe(0);
			expect(skill.renderChargeTime).toBe(0);
		});

		it('stores reference to owner', () => {
			const owner = makeMockOwner();
			const skill = new Skill(makeSkillData(), owner);

			expect(skill.owner).toBe(owner);
		});
	});

	describe('calculateDamage', () => {
		it('returns baseDamage when there are no multipliers', () => {
			const skill = new Skill(makeSkillData({ baseDamage: 15 }), makeMockOwner());
			expect(skill.calculateDamage()).toBe(15);
		});

		it('adds attribute-scaled damage from multipliers', () => {
			const multipliers: IAttributeMultiplier[] = [{ attributeId: EAttribute.Strength, multiplier: 1.5 }];
			const owner = makeMockOwner([[EAttribute.Strength, 20]]);
			const skill = new Skill(makeSkillData({ baseDamage: 10, damageMultipliers: multipliers }), owner);

			expect(skill.calculateDamage()).toBe(10 + 20 * 1.5);
		});

		it('sums multiple multipliers', () => {
			const multipliers: IAttributeMultiplier[] = [
				{ attributeId: EAttribute.Strength, multiplier: 1.0 },
				{ attributeId: EAttribute.Agility, multiplier: 0.5 }
			];
			const owner = makeMockOwner([
				[EAttribute.Strength, 10],
				[EAttribute.Agility, 20]
			]);
			const skill = new Skill(makeSkillData({ baseDamage: 5, damageMultipliers: multipliers }), owner);

			expect(skill.calculateDamage()).toBe(5 + 10 * 1.0 + 20 * 0.5);
		});

		it('handles zero attribute value', () => {
			const multipliers: IAttributeMultiplier[] = [{ attributeId: EAttribute.Strength, multiplier: 2.0 }];
			const owner = makeMockOwner();
			const skill = new Skill(makeSkillData({ baseDamage: 10, damageMultipliers: multipliers }), owner);

			expect(skill.calculateDamage()).toBe(10);
		});
	});
});
