import { describe, it, expect } from 'vitest';
import { EDamageType, type ISkillDamagePortion } from '$lib/api';
import { isFielded, isSkillDormant, newlyDormantSkills } from '$lib/battle';

/* The weapon-match gate (#1342) — the frontend mirror of `BattleLoadout.IsFielded`. These pure helpers
   back both the battle assembly (`Battler.fillSkills`) and the Skills-screen grey-out / inventory pre-swap
   warning, so they must agree on exactly which skills a weapon dims. */

const skill = (...portions: ISkillDamagePortion[]) => ({ damagePortions: portions });
const portion = (type: EDamageType, weight = 1): ISkillDamagePortion => ({ type, weight });

describe('isFielded', () => {
	it('fields a weapon-leaf skill only when it matches the equipped weapon', () => {
		expect(isFielded(EDamageType.Sword, EDamageType.Sword)).toBe(true);
		expect(isFielded(EDamageType.Sword, EDamageType.Axe)).toBe(false);
		expect(isFielded(EDamageType.Unarmed, EDamageType.Unarmed)).toBe(true);
		expect(isFielded(EDamageType.Unarmed, EDamageType.Sword)).toBe(false);
	});

	it('always fields weapon-agnostic types regardless of the equipped weapon', () => {
		for (const type of [EDamageType.Physical, EDamageType.Fire, EDamageType.Bleed, EDamageType.Poison]) {
			expect(isFielded(type, EDamageType.Sword)).toBe(true);
			expect(isFielded(type, EDamageType.Unarmed)).toBe(true);
		}
	});
});

describe('isSkillDormant', () => {
	it('keys off the skill’s primary (highest-weight) portion', () => {
		// A mostly-Sword hybrid reads as a Sword skill: dormant under an Axe, fielded under a Sword.
		const hybrid = skill(portion(EDamageType.Sword, 0.7), portion(EDamageType.Fire, 0.3));
		expect(isSkillDormant(hybrid, EDamageType.Axe)).toBe(true);
		expect(isSkillDormant(hybrid, EDamageType.Sword)).toBe(false);
	});

	it('never dims a weapon-agnostic skill', () => {
		expect(isSkillDormant(skill(portion(EDamageType.Fire)), EDamageType.Sword)).toBe(false);
	});
});

describe('newlyDormantSkills', () => {
	const cleave = skill(portion(EDamageType.Axe));
	const slash = skill(portion(EDamageType.Sword));
	const jab = skill(portion(EDamageType.Unarmed));
	const ember = skill(portion(EDamageType.Fire));

	it('returns the skills a weapon swap newly dims, leaving agnostic + relit skills alone', () => {
		// Bare-handed (Unarmed) → equipping a Sword: the fielded Unarmed skill dims; the Sword skill lights
		// up (was dormant, now fielded) and the Fire skill is agnostic — neither is "newly dormant".
		const dimmed = newlyDormantSkills([jab, slash, ember], EDamageType.Unarmed, EDamageType.Sword);
		expect(dimmed).toEqual([jab]);
	});

	it('does not re-list a skill that was already dormant under the current weapon', () => {
		// Wielding a Sword (Axe skill already dormant); swapping to a Club dims nothing new.
		const dimmed = newlyDormantSkills([cleave], EDamageType.Sword, EDamageType.Club);
		expect(dimmed).toEqual([]);
	});

	it('is empty when the weapon type is unchanged', () => {
		expect(newlyDormantSkills([cleave, slash], EDamageType.Sword, EDamageType.Sword)).toEqual([]);
	});

	it('reports a skill that matched the old weapon and not the new one', () => {
		// Sword → Axe: the Sword skill dims, the Axe skill lights up (not "newly dormant").
		const dimmed = newlyDormantSkills([cleave, slash], EDamageType.Sword, EDamageType.Axe);
		expect(dimmed).toEqual([slash]);
	});
});
