import { ApiRequest, fetchSocketData, type ISkill } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { attributeChanges, persistEntity } from '../save-helpers';
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
	newItem: (id) => ({
		id,
		name: '',
		baseDamage: 10,
		cooldownMs: 2000,
		iconPath: '',
		description: '',
		damageMultipliers: []
	}),
	meta: (s) => [
		['dmg', s.baseDamage],
		['×mult', s.damageMultipliers.length],
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
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: (s) => ({ ...s, damageMultipliers: [] }),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditSkills', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					const changes = attributeChanges(record.damageMultipliers, baseline?.damageMultipliers, 'multiplier');
					if (changes.length) {
						await ApiRequest.post('AdminTools/SetSkillMultipliers', { id, changes });
					}
				}
			]
		})
};
