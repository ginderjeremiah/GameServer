import {
	ApiRequest,
	EAttribute,
	type EEquipmentSlot,
	EModifierType,
	ESkillAcquisition,
	fetchSocketData,
	type IClass
} from '$lib/api';
import { hasFlag } from '$lib/common';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, guardedSave, persistEntity } from '../save-helpers';
import { attributeDistributionSection } from './attribute-table-sections';
import { firstFree } from './helpers';
import { chipsSection, type EntityConfig } from './types';

/**
 * A class with its optional signature-passive scaling attribute widened to a plain number so the select can
 * use a "None" sentinel (-1) — mirroring how items handle their optional granted-skill FK.
 */
export interface WorkbenchClass extends Omit<IClass, 'passiveScalingAttributeId'> {
	passiveScalingAttributeId: number;
}

// Classes load over the socket; the admin filter invalidates this cache on every write server-side, so a
// plain refetch returns the freshly-saved list. Written through to staticData.classes so retire-confirm's
// reference computation (starter-skill/starter-equipment groups) sees post-save edits (#1633). staticData.classes
// stays raw (undefined = none) for the game/other screens, mirroring item.ts; the optional scaling attribute is
// normalised to the select's "None" sentinel (-1) only in the returned editable copy.
const refresh = async (): Promise<WorkbenchClass[]> => {
	const classes = await fetchSocketData('GetClasses');
	staticData.classes = classes;
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
		designerNotes: '',
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
					reqMsg: 'Missing name',
					maxLength: 50
				},
				{
					key: 'word',
					label: 'Word of Power',
					type: 'text',
					placeholder: 'Conlang label (decorative)…',
					grow: true,
					required: true,
					reqMsg: 'Missing word of power',
					maxLength: 50
				},
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe the class shown to players…',
					grow: true,
					required: true,
					reqMsg: 'No description',
					maxLength: 500
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this class exists — authoring notes (never shown to players)…',
					grow: true,
					maxLength: 2000
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
					type: 'attribute',
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
					type: 'attribute',
					options: reference.optionalAttributeOptions,
					width: 200
				},
				{ key: 'passiveScalingAmount', label: 'Scaling Amount', type: 'number', width: 150, allowNegative: true }
			]
		},
		chipsSection<WorkbenchClass>()({
			key: 'starterSkills',
			label: 'Starter Skills',
			glyph: 'rune',
			desc: 'Skills granted at character creation',
			count: (c) => c.starterSkillIds.length,
			// A starter skill that's since lost its Player flag stays visible as a removable chip
			// (the catalogue's `addable` filter only blocks new authoring); AdminClasses.SetClassStarterSkills
			// (FindStarterSkillFlagViolation) hard-rejects the whole list if any assigned skill lacks the
			// flag, so this blocks Save too.
			warn: (c) => {
				const flagLost = c.starterSkillIds
					.map((id) => staticData.skills?.[id])
					.find((skill) => skill && !hasFlag(skill.acquisition, ESkillAcquisition.Player));
				if (flagLost) {
					return { message: `'${flagLost.name}' is no longer flagged as Player-acquirable`, blocking: true };
				}
				return c.starterSkillIds.length ? null : 'No starter skills';
			},
			kind: 'chips',
			itemsKey: 'starterSkillIds',
			// Only Player-flagged skills can be newly assigned (the backend enforces this too); an
			// already-assigned skill that lost the flag stays visible as a removable chip.
			catalogue: () =>
				reference.skillCatalogue().map((s) => ({ ...s, addable: hasFlag(s.acquisition, ESkillAcquisition.Player) })),
			labelOf: (s) => s.name,
			metaOf: (s) => `${s.baseDamage} dmg`,
			emptyIcon: 'rune',
			emptyTitle: 'No starter skills',
			emptySub: 'This class grants no skills at creation.',
			addLabel: 'Add starter skill…'
		}),
		{
			key: 'starterEquipment',
			label: 'Starter Equipment',
			glyph: 'box',
			desc: 'Items equipped at character creation',
			count: (c) => c.starterEquipment.length,
			// Hard-rejected by AdminClasses.SetStarterEquipment, so it blocks Save (#2217).
			warn: (c) => {
				const mismatch = c.starterEquipment.find(
					(e) => reference.itemCategoryOf(e.itemId) !== reference.equipmentSlotCategory(e.equipmentSlot)
				);
				return mismatch ? { message: `Item doesn't match its equipment slot's category`, blocking: true } : null;
			},
			kind: 'table',
			itemsKey: 'starterEquipment',
			rowKey: 'equipmentSlot',
			addLabel: 'Add equipment',
			emptyIcon: 'box',
			emptyTitle: 'No starter equipment',
			emptySub: 'This class starts with nothing equipped.',
			newRow: (c) => {
				const equipmentSlot = firstFree(
					c.starterEquipment.map((e) => e.equipmentSlot),
					reference.equipmentSlotOptions()
				);
				return {
					equipmentSlot,
					itemId: reference.itemOptionsForSlot(equipmentSlot)[0]?.value ?? 0
				};
			},
			columns: [
				{
					key: 'equipmentSlot',
					label: 'Slot',
					type: 'select',
					options: reference.equipmentSlotOptions,
					min: 160,
					unique: true
				},
				{
					key: 'itemId',
					label: 'Item',
					type: 'select',
					options: (current, row) => reference.itemOptionsForSlot((row?.equipmentSlot as EEquipmentSlot) ?? 0, current),
					min: 220
				}
			]
		},
		attributeDistributionSection<WorkbenchClass>({
			key: 'attributes',
			itemsKey: 'attributeDistributions',
			desc: 'Locked-base stat distribution per level',
			emptySub: 'This class has no stat distribution yet.'
		})
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
				designerNotes: c.designerNotes,
				starterSkillIds: [],
				starterEquipment: [],
				attributeDistributions: [],
				retiredAt: c.retiredAt
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditClasses', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) =>
					guardedSave(childChanged(record.starterSkillIds, baseline?.starterSkillIds), () =>
						ApiRequest.post('AdminTools/SetClassStarterSkills', {
							classId: id,
							skillIds: record.starterSkillIds
						})
					),
				async (id, record, baseline) =>
					guardedSave(childChanged(record.starterEquipment, baseline?.starterEquipment), () =>
						ApiRequest.post('AdminTools/SetClassStarterEquipment', {
							classId: id,
							equipment: record.starterEquipment
						})
					),
				async (id, record, baseline) =>
					guardedSave(childChanged(record.attributeDistributions, baseline?.attributeDistributions), () =>
						ApiRequest.post('AdminTools/SetClassAttributeDistributions', {
							classId: id,
							attributeDistributions: record.attributeDistributions
						})
					)
			]
		})
};
