import {
	ApiRequest,
	EAttribute,
	EDamageType,
	EModifierType,
	ERarity,
	ESkillAcquisition,
	ESkillEffectTarget,
	fetchSocketData,
	type ISkill
} from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import {
	attributeChanges,
	damagePortionChanges,
	guardedSave,
	persistEntity,
	skillEffectChanges
} from '../save-helpers';
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
		// The per-skill opt-in crit enabler (#1453): 0 by default, so a new skill never crits until authored.
		criticalChance: 0,
		cooldownMs: 2000,
		iconPath: '',
		rarityId: ERarity.Common,
		word: '',
		pronunciation: '',
		translation: '',
		designerNotes: '',
		// New skills default to player-acquirable; re-flag Item/Enemy skills as needed.
		acquisition: ESkillAcquisition.Player,
		description: '',
		// New skills deal a single full-weight Physical portion; add/re-type portions as needed (#1343).
		damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
		damageMultipliers: [],
		effects: []
	}),
	listBadge: (s) => reference.rarityName(s.rarityId),
	badgeColor: (s) => reference.rarityColor(s.rarityId),
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
				{
					key: 'criticalChance',
					label: 'Critical Chance',
					type: 'number',
					suffix: '0–1',
					width: 150
				},
				{ key: 'cooldownMs', label: 'Cooldown', type: 'number', suffix: 'ms', width: 150 },
				{ key: 'rarityId', label: 'Rarity', type: 'select', options: reference.rarityOptions, width: 170 },
				{
					key: 'iconPath',
					label: 'Icon Path',
					type: 'text',
					placeholder: 'skills/icon.png',
					grow: true,
					maxLength: 50
				},
				{ key: 'word', label: 'Word of Power', type: 'text', placeholder: 'sijren', width: 200, maxLength: 50 },
				{
					key: 'pronunciation',
					label: 'Pronunciation',
					type: 'text',
					placeholder: 'sij·ren',
					width: 200,
					maxLength: 50
				},
				{
					key: 'translation',
					label: 'Translation',
					type: 'text',
					placeholder: 'The Riven Frost',
					grow: true,
					maxLength: 100
				},
				{
					key: 'acquisition',
					label: 'Acquisition (channels allowed to grant this skill)',
					type: 'flags',
					flags: [
						{ label: 'Player', value: ESkillAcquisition.Player },
						{ label: 'Item', value: ESkillAcquisition.Item },
						{ label: 'Enemy', value: ESkillAcquisition.Enemy }
					]
				},
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe what this skill does…',
					grow: true,
					required: true,
					reqMsg: 'No description',
					maxLength: 500
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this skill exists — authoring notes (never shown to players)…',
					grow: true,
					maxLength: 2000
				}
			]
		},
		{
			key: 'portions',
			label: 'Damage Types',
			glyph: 'bolt',
			desc: 'The weighted leaf-type split this skill’s direct hits deal',
			count: (s) => s.damagePortions.length,
			// Validate authoring intent the backend also guards: at least one portion, all weights positive.
			// Both conditions are hard-rejected by AdminSkills.SetPortions, so they block Save (#2217).
			warn: (s) =>
				s.damagePortions.length === 0
					? { message: 'No damage portions', blocking: true }
					: s.damagePortions.some((p) => p.weight <= 0)
						? { message: 'Portion weights must be positive', blocking: true }
						: null,
			kind: 'table',
			itemsKey: 'damagePortions',
			rowKey: 'type',
			addLabel: 'Add portion',
			emptyIcon: 'bolt',
			emptyTitle: 'No damage portions',
			emptySub: 'A skill must deal at least one damage type.',
			newRow: (s) => ({
				type: firstFree(
					s.damagePortions.map((p) => p.type),
					reference.damageTypeOptions()
				),
				weight: 1
			}),
			// "Even split" equalizes the weights (1 each); fire-time normalization makes that an equal share.
			actions: [{ label: 'Even split', glyph: 'bars', apply: (rows) => rows.forEach((r) => (r.weight = 1)) }],
			columns: [
				{
					key: 'type',
					label: 'Damage Type',
					type: 'select',
					options: reference.damageTypeOptions,
					min: 180,
					unique: true
				},
				{ key: 'weight', label: 'Weight', type: 'number', align: 'r', width: 110 },
				{ key: '__share', label: 'Share', type: 'share', width: 150, weightKey: 'weight' }
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
			rowKey: 'attributeId',
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
					type: 'attribute',
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
			rowKey: 'id',
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
				durationMs: 3000,
				scalingAttributeId: EAttribute.Strength,
				scalingAmount: 0
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
					type: 'attribute',
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
				{ key: 'durationMs', label: 'Duration (ms)', type: 'number', align: 'r', width: 130 },
				{
					key: 'scalingAttributeId',
					label: 'Scales with',
					type: 'attribute',
					options: reference.attributeOptions,
					min: 170
				},
				{ key: 'scalingAmount', label: 'Per point', type: 'number', align: 'r', width: 110, allowNegative: true }
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: (s) => ({ ...s, damagePortions: [], damageMultipliers: [], effects: [] }),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditSkills', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					const changes = damagePortionChanges(record.damagePortions, baseline?.damagePortions);
					return guardedSave(changes.length > 0, () => ApiRequest.post('AdminTools/SetSkillPortions', { id, changes }));
				},
				async (id, record, baseline) => {
					const changes = attributeChanges(record.damageMultipliers, baseline?.damageMultipliers, 'multiplier');
					return guardedSave(changes.length > 0, () =>
						ApiRequest.post('AdminTools/SetSkillMultipliers', { id, changes })
					);
				},
				async (id, record, baseline) => {
					const changes = skillEffectChanges(record.effects, baseline?.effects);
					return guardedSave(changes.length > 0, () => ApiRequest.post('AdminTools/SetSkillEffects', { id, changes }));
				}
			]
		})
};
