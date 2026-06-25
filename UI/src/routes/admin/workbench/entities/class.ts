import { ApiRequest, EAttribute, EModifierType, ESkillAcquisition, fetchSocketData, type IClass } from '$lib/api';
import { hasFlag } from '$lib/common';
import { reference } from '../reference.svelte';
import { childChanged, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import type { EntityConfig } from './types';

/**
 * A class with its optional signature-passive scaling attribute widened to a plain number so the select can
 * use a "None" sentinel (-1) — mirroring how items handle their optional granted-skill FK.
 */
export interface WorkbenchClass extends Omit<IClass, 'passiveScalingAttributeId'> {
	passiveScalingAttributeId: number;
}

// Classes load over the socket; the admin filter invalidates this cache on every write server-side, so a
// plain refetch returns the freshly-saved list. The optional scaling attribute is normalised to the select's
// "None" sentinel (-1) for the editable copy.
const refresh = async (): Promise<WorkbenchClass[]> => {
	const classes = await fetchSocketData('GetClasses');
	return classes.map((c) => ({ ...c, passiveScalingAttributeId: c.passiveScalingAttributeId ?? -1 }));
};

export const classEntity: EntityConfig<WorkbenchClass> = {
	key: 'classes',
	label: 'Classes',
	singular: 'Class',
	glyph: 'gauge',
	blankName: 'Unnamed class',
	retireable: true,
	newItem: (id) => ({
		id,
		name: '',
		description: '',
		word: '',
		passiveAttributeId: EAttribute.Strength,
		passiveAmount: 0,
		passiveScalingAttributeId: -1,
		passiveScalingAmount: 0,
		passiveModifierType: EModifierType.Additive,
		starterSkillIds: [],
		starterEquipment: [],
		attributeDistributions: []
	}),
	headline: (c) => (c.word ? c.word : ''),
	meta: (c) => [
		['skill', c.starterSkillIds.length],
		['gear', c.starterEquipment.length],
		['attr', c.attributeDistributions.length]
	],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name, word of power & description',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Class Name',
					type: 'text',
					placeholder: 'Name this class…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{
					key: 'word',
					label: 'Word of Power',
					type: 'text',
					placeholder: 'Conlang label (decorative)…',
					grow: true,
					required: true,
					reqMsg: 'Missing word of power'
				},
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe the class shown to players…',
					grow: true,
					required: true,
					reqMsg: 'No description'
				}
			]
		},
		{
			key: 'passive',
			label: 'Passive',
			glyph: 'bolt',
			desc: 'The class signature passive',
			kind: 'fields',
			fields: [
				{
					key: 'passiveAttributeId',
					label: 'Attribute',
					type: 'select',
					options: reference.attributeOptions,
					width: 200
				},
				{ key: 'passiveAmount', label: 'Amount', type: 'number', width: 130, allowNegative: true },
				{
					key: 'passiveModifierType',
					label: 'Modifier',
					type: 'select',
					options: reference.modifierTypeOptions,
					width: 160
				},
				{
					key: 'passiveScalingAttributeId',
					label: 'Scales With',
					type: 'select',
					options: reference.optionalAttributeOptions,
					width: 200
				},
				{ key: 'passiveScalingAmount', label: 'Scaling Amount', type: 'number', width: 150, allowNegative: true }
			]
		},
		{
			key: 'starterSkills',
			label: 'Starter Skills',
			glyph: 'rune',
			desc: 'Skills granted at character creation',
			count: (c) => c.starterSkillIds.length,
			warn: (c) => (c.starterSkillIds.length ? null : 'No starter skills'),
			kind: 'chips',
			itemsKey: 'starterSkillIds',
			// Only Player-flagged skills can be newly assigned (the backend enforces this too); an
			// already-assigned skill that lost the flag stays visible as a removable chip.
			catalogue: () =>
				reference.skillCatalogue().map((s) => ({ ...s, addable: hasFlag(s.acquisition, ESkillAcquisition.Player) })),
			labelOf: (s) => s.name,
			metaOf: (s) => `${(s as unknown as { baseDamage: number }).baseDamage} dmg`,
			emptyIcon: 'rune',
			emptyTitle: 'No starter skills',
			emptySub: 'This class grants no skills at creation.',
			addLabel: 'Add starter skill…'
		},
		{
			key: 'starterEquipment',
			label: 'Starter Equipment',
			glyph: 'box',
			desc: 'Items equipped at character creation',
			count: (c) => c.starterEquipment.length,
			kind: 'table',
			itemsKey: 'starterEquipment',
			rowKey: 'equipmentSlot',
			addLabel: 'Add equipment',
			emptyIcon: 'box',
			emptyTitle: 'No starter equipment',
			emptySub: 'This class starts with nothing equipped.',
			newRow: (c) => ({
				equipmentSlot: firstFree(
					c.starterEquipment.map((e) => e.equipmentSlot),
					reference.equipmentSlotOptions()
				),
				itemId: reference.itemOptions()[0]?.value ?? 0
			}),
			columns: [
				{
					key: 'equipmentSlot',
					label: 'Slot',
					type: 'select',
					options: reference.equipmentSlotOptions,
					min: 160,
					unique: true
				},
				{ key: 'itemId', label: 'Item', type: 'select', options: reference.itemOptions, min: 220 }
			]
		},
		{
			key: 'attributes',
			label: 'Attributes',
			glyph: 'bars',
			desc: 'Locked-base stat distribution per level',
			count: (c) => c.attributeDistributions.length,
			warn: (c) => (c.attributeDistributions.length ? null : 'No attribute distribution'),
			kind: 'table',
			itemsKey: 'attributeDistributions',
			rowKey: 'attributeId',
			addLabel: 'Add attribute',
			emptyIcon: 'bars',
			emptyTitle: 'No attributes set',
			emptySub: 'This class has no stat distribution yet.',
			newRow: (c) => ({
				attributeId: firstFree(
					c.attributeDistributions.map((a) => a.attributeId),
					reference.attributeOptions()
				),
				baseAmount: 0,
				amountPerLevel: 0
			}),
			columns: [
				{
					key: 'attributeId',
					label: 'Attribute',
					type: 'select',
					options: reference.attributeOptions,
					min: 190,
					unique: true
				},
				{ key: 'baseAmount', label: 'Base', type: 'number', align: 'r', width: 110, allowNegative: true },
				{ key: 'amountPerLevel', label: 'Per Level', type: 'number', align: 'r', width: 110, allowNegative: true }
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			// The identity DTO carries the scalar fields (incl. the signature passive); the "None" sentinel
			// (-1) maps back to no scaling attribute, and the child collections are saved through their own
			// endpoints, so they are cleared here.
			toPrimaryDto: (c) => ({
				id: c.id,
				name: c.name,
				description: c.description,
				word: c.word,
				passiveAttributeId: c.passiveAttributeId,
				passiveAmount: c.passiveAmount,
				passiveScalingAttributeId: c.passiveScalingAttributeId === -1 ? undefined : c.passiveScalingAttributeId,
				passiveScalingAmount: c.passiveScalingAmount,
				passiveModifierType: c.passiveModifierType,
				starterSkillIds: [],
				starterEquipment: [],
				attributeDistributions: [],
				retiredAt: c.retiredAt
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditClasses', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					if (childChanged(record.starterSkillIds, baseline?.starterSkillIds)) {
						await ApiRequest.post('AdminTools/SetClassStarterSkills', {
							classId: id,
							skillIds: record.starterSkillIds
						});
					}
				},
				async (id, record, baseline) => {
					if (childChanged(record.starterEquipment, baseline?.starterEquipment)) {
						await ApiRequest.post('AdminTools/SetClassStarterEquipment', {
							classId: id,
							equipment: record.starterEquipment
						});
					}
				},
				async (id, record, baseline) => {
					if (childChanged(record.attributeDistributions, baseline?.attributeDistributions)) {
						await ApiRequest.post('AdminTools/SetClassAttributeDistributions', {
							classId: id,
							attributeDistributions: record.attributeDistributions
						});
					}
				}
			]
		})
};
