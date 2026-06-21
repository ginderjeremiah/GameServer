/* Shared fixtures for the Fight screen tests. Not a test file (no
   .test/.spec suffix) so vitest does not collect it. */

import { EAttribute, ESkillAcquisition, type IBattlerAttribute, type ISkill } from '$lib/api';
import { Battler, Skill } from '$lib/battle';

/** Builds a real Skill bound to an owner, overriding only what a test cares about. */
export const makeSkill = (owner: Battler, over: Partial<ISkill> = {}): Skill =>
	new Skill(
		{
			id: 1,
			name: 'Slash',
			baseDamage: 10,
			damageMultipliers: [],
			effects: [],
			description: 'A basic slash.',
			cooldownMs: 1000,
			iconPath: '/icons/slash.png',
			acquisition: ESkillAcquisition.Player,
			...over
		},
		owner
	);

interface BattlerOverrides {
	name?: string;
	level?: number;
	attributes?: IBattlerAttribute[];
	currentHealth?: number;
}

/** Builds a real Battler with exact (non-derived) attribute values so the health and
 *  damage maths in the cards/tooltip stay predictable. Skills, when a test needs them,
 *  are attached by the caller (e.g. `battler.skills = [makeSkill(battler)]`). */
export const makeBattler = ({
	name = 'Aelara',
	level = 12,
	attributes = [{ attributeId: EAttribute.MaxHealth, amount: 100 }],
	currentHealth
}: BattlerOverrides = {}): Battler => {
	const battler = new Battler();
	battler.name = name;
	battler.level = level;
	// setData(.., false) skips the derived-stat pass, so the values are exactly what we pass.
	battler.attributes.setData(attributes, false);
	battler.currentHealth = currentHealth ?? battler.attributes.getValue(EAttribute.MaxHealth);
	return battler;
};
