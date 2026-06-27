import { ApiRequest, EItemCategory, ERarity, fetchSocketData, type IItem } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { attributeChanges, childChanged, modSlotChanges, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import { tagsSection } from './tags-section';
import type { EntityConfig } from './types';

const refresh = async (): Promise<IItem[]> => {
	const items = await fetchSocketData('GetItems');
	staticData.items = items;
	// Normalise the optional FK fields to the select's "None" sentinel (-1) for the editable copy;
	// staticData.items stays raw (null = none) for the game/other screens.
	return items.map((item) => ({
		...item,
		grantedSkillId: item.grantedSkillId ?? -1,
		requiredProficiencyId: item.requiredProficiencyId ?? -1
	}));
};

export const itemEntity: EntityConfig<IItem> = {
	key: 'items',
	label: 'Items',
	singular: 'Item',
	glyph: 'box',
	blankName: 'Unnamed item',
	retireable: true,
	newItem: (id) => ({
		id,
		name: '',
		description: '',
		itemCategoryId: EItemCategory.Helm,
		rarityId: ERarity.Common,
		iconPath: '',
		grantedSkillId: -1,
		requiredProficiencyId: -1,
		requiredProficiencyLevel: 1,
		attributes: [],
		modSlots: [],
		tags: []
	}),
	listBadge: (it) => reference.rarityName(it.rarityId),
	badgeColor: (it) => reference.rarityColor(it.rarityId),
	meta: (it) => [
		['', reference.itemCategoryName(it.itemCategoryId)],
		['attr', it.attributes.length],
		['tag', it.tags.length]
	],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name, category & rarity',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Item Name',
					type: 'text',
					placeholder: 'Name this item…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{
					key: 'itemCategoryId',
					label: 'Category',
					type: 'select',
					options: reference.itemCategoryOptions,
					width: 170
				},
				{ key: 'rarityId', label: 'Rarity', type: 'select', options: reference.rarityOptions, width: 170 },
				{
					key: 'iconPath',
					label: 'Icon Path',
					type: 'text',
					placeholder: 'items/icon.png',
					grow: true,
					required: true,
					reqMsg: 'No icon path'
				},
				{
					key: 'grantedSkillId',
					label: 'Granted Skill',
					type: 'select',
					options: reference.grantedSkillOptions,
					width: 240
				},
				{
					key: 'requiredProficiencyId',
					label: 'Required Proficiency',
					type: 'select',
					options: reference.requiredProficiencyOptions,
					width: 240
				},
				{ key: 'requiredProficiencyLevel', label: 'Required Level', type: 'number', width: 150 },
				{ key: 'description', label: 'Description', type: 'textarea', placeholder: 'Flavor text…', grow: true }
			]
		},
		{
			key: 'attributes',
			label: 'Attributes',
			glyph: 'bars',
			desc: 'Flat stat bonuses granted',
			count: (it) => it.attributes.length,
			warn: (it) => (it.attributes.length ? null : 'No attributes'),
			kind: 'table',
			itemsKey: 'attributes',
			rowKey: 'attributeId',
			addLabel: 'Add bonus',
			emptyIcon: 'bars',
			emptyTitle: 'No attribute bonuses',
			emptySub: 'This item grants no stats.',
			newRow: (it) => ({
				attributeId: firstFree(
					it.attributes.map((a) => a.attributeId),
					reference.attributeOptions()
				),
				amount: 1
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
				{ key: 'amount', label: 'Amount', type: 'number', align: 'r', width: 120, allowNegative: true }
			]
		},
		{
			key: 'modSlots',
			label: 'Mod Slots',
			glyph: 'box',
			desc: 'Slots that accept item mods',
			count: (it) => it.modSlots.length,
			kind: 'table',
			itemsKey: 'modSlots',
			rowKey: 'id',
			addLabel: 'Add slot',
			emptyIcon: 'box',
			emptyTitle: 'No mod slots',
			emptySub: 'This item can’t hold mods.',
			newRow: () => ({ id: 0, itemId: 0, itemModSlotTypeId: 1 }),
			columns: [
				{ key: 'itemModSlotTypeId', label: 'Slot Type', type: 'select', options: reference.modTypeOptions, min: 200 }
			]
		},
		tagsSection<IItem>()
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			// Map the "None" sentinel (-1) back to no grant before persisting; child collections are saved
			// through their own endpoints, so the primary DTO clears them.
			toPrimaryDto: (it) => ({
				...it,
				grantedSkillId: it.grantedSkillId === -1 ? undefined : it.grantedSkillId,
				// Map the "None" sentinel (-1) back to no gate; the backend ignores the level when ungated.
				requiredProficiencyId: it.requiredProficiencyId === -1 ? undefined : it.requiredProficiencyId,
				attributes: [],
				modSlots: [],
				tags: []
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditItems', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					const changes = attributeChanges(record.attributes, baseline?.attributes, 'amount');
					if (changes.length) {
						await ApiRequest.post('AdminTools/AddEditItemAttributes', { id, changes });
					}
				},
				async (id, record, baseline) => {
					const changes = modSlotChanges(record.modSlots, baseline?.modSlots, id);
					if (changes.length) {
						await ApiRequest.post('AdminTools/AddEditItemModSlots', changes);
					}
				},
				async (id, record, baseline) => {
					if (childChanged(record.tags, baseline?.tags)) {
						await ApiRequest.post('AdminTools/SetTagsForItem', { id, tagIds: record.tags });
					}
				}
			]
		})
};
