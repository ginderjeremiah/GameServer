import { ApiRequest, EAttribute, EModifierType, ESkillEffectTarget, fetchSocketData, type ISkill } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { attributeChanges, persistEntity, skillEffectChanges } from '../save-helpers';
import { firstFree } from './helpers';
import type { EntityConfig } from './types';

const refresh = async (): Promise<ISkill[]> => {
	const skills = await fetchSocketData('GetSkills');
	staticData.skills = skills;
	return skills;
};

export const skillEntity: EntityConfig<ISkill> = {
	key: 'skills',
	label: 'Skills',
	singular: 'Skill',
	glyph: 'bolt',
	blankName: 'Unnamed skill',
	retireable: true,
	newItem: (id) => ({
		id,
		name: '',
		baseDamage: 10,
		cooldownMs: 2000,
		iconPath: '',
		description: '',
		damageMultipliers: [],
		effects: []
	}),
	meta: (s) => [
		['dmg', s.baseDamage],
		['×mult', s.damageMultipliers.length],
		['fx', s.effects.length],
		['cd', `${(s.cooldownMs / 1000).toFixed(1)}s`]
	],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name, damage, cooldown & description',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Skill Name',
					type: 'text',
					placeholder: 'Name this skill…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{ key: 'baseDamage', label: 'Base Damage', type: 'number', suffix: 'dmg', width: 150 },
				{ key: 'cooldownMs', label: 'Cooldown', type: 'number', suffix: 'ms', width: 150 },
				{ key: 'iconPath', label: 'Icon Path', type: 'text', placeholder: 'skills/icon.png', grow: true },
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe what this skill does…',
					grow: true,
					required: true,
					reqMsg: 'No description'
				}
			]
		},
		{
			key: 'multipliers',
			label: 'Multipliers',
			glyph: 'multiply',
			desc: 'How attributes scale this skill',
			count: (s) => s.damageMultipliers.length,
			warn: (s) => (s.damageMultipliers.length ? null : 'No damage multipliers'),
			kind: 'table',
			itemsKey: 'damageMultipliers',
			addLabel: 'Add multiplier',
			emptyIcon: 'multiply',
			emptyTitle: 'No damage multipliers',
			emptySub: 'Damage won’t scale with any attribute.',
			newRow: (s) => ({
				attributeId: firstFree(
					s.damageMultipliers.map((m) => m.attributeId),
					reference.attributeOptions()
				),
				multiplier: 1
			}),
			columns: [
				{
					key: 'attributeId',
					label: 'Attribute',
					type: 'select',
					options: reference.attributeOptions,
					min: 200,
					unique: true
				},
				{ key: 'multiplier', label: 'Multiplier ×', type: 'number', align: 'r', width: 120, allowNegative: true }
			]
		},
		{
			key: 'effects',
			label: 'Effects',
			glyph: 'bolt',
			desc: 'Timed attribute buffs/debuffs applied when the skill fires',
			count: (s) => s.effects.length,
			kind: 'table',
			itemsKey: 'effects',
			addLabel: 'Add effect',
			emptyIcon: 'bolt',
			emptyTitle: 'No effects',
			emptySub: 'This skill applies no timed attribute modifiers.',
			newRow: () => ({
				id: 0,
				target: ESkillEffectTarget.Opponent,
				attributeId: EAttribute.Strength,
				modifierTypeId: EModifierType.Additive,
				amount: 0,
				durationMs: 3000
			}),
			columns: [
				{
					key: 'target',
					label: 'Target',
					type: 'select',
					options: reference.skillEffectTargetOptions,
					width: 130
				},
				{
					key: 'attributeId',
					label: 'Attribute',
					type: 'select',
					options: reference.attributeOptions,
					min: 170
				},
				{
					key: 'modifierTypeId',
					label: 'Modifier',
					type: 'select',
					options: reference.modifierTypeOptions,
					width: 140
				},
				{ key: 'amount', label: 'Amount', type: 'number', align: 'r', width: 100, allowNegative: true },
				{ key: 'durationMs', label: 'Duration (ms)', type: 'number', align: 'r', width: 130 }
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: (s) => ({ ...s, damageMultipliers: [], effects: [] }),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditSkills', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					const changes = attributeChanges(record.damageMultipliers, baseline?.damageMultipliers, 'multiplier');
					if (changes.length) {
						await ApiRequest.post('AdminTools/SetSkillMultipliers', { id, changes });
					}
				},
				async (id, record, baseline) => {
					const changes = skillEffectChanges(record.effects, baseline?.effects);
					if (changes.length) {
						await ApiRequest.post('AdminTools/SetSkillEffects', { id, changes });
					}
				}
			]
		})
};
