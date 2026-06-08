import { ApiRequest, EItemModType, ERarity, fetchSocketData, type IItemMod } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { attributeChanges, childChanged, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import { tagsSection } from './tags-section';
import type { EntityConfig } from './types';

const refresh = async (): Promise<IItemMod[]> => {
	const itemMods = await fetchSocketData('GetItemMods');
	staticData.itemMods = itemMods;
	return itemMods;
};

export const itemModEntity: EntityConfig<IItemMod> = {
	key: 'itemMods',
	label: 'Item Mods',
	singular: 'Item Mod',
	glyph: 'rune',
	blankName: 'Unnamed mod',
	newItem: (id) => ({
		id,
		name: '',
		description: '',
		itemModTypeId: EItemModType.Component,
		rarityId: ERarity.Common,
		attributes: [],
		tags: []
	}),
	listBadge: (m) => reference.rarityName(m.rarityId),
	badgeColor: (m) => reference.rarityColor(m.rarityId),
	meta: (m) => [
		['', reference.modTypeName(m.itemModTypeId)],
		['attr', m.attributes.length],
		['tag', m.tags.length]
	],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name, type, rarity & description',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Mod Name',
					type: 'text',
					placeholder: 'Name this mod…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{ key: 'itemModTypeId', label: 'Type', type: 'select', options: reference.modTypeOptions, width: 170 },
				{ key: 'rarityId', label: 'Rarity', type: 'select', options: reference.rarityOptions, width: 170 },
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe this mod…',
					grow: true,
					required: true,
					reqMsg: 'No description'
				}
			]
		},
		{
			key: 'attributes',
			label: 'Attributes',
			glyph: 'bars',
			desc: 'Flat stat bonuses granted',
			count: (m) => m.attributes.length,
			warn: (m) => (m.attributes.length ? null : 'No attributes'),
			kind: 'table',
			itemsKey: 'attributes',
			addLabel: 'Add bonus',
			emptyIcon: 'bars',
			emptyTitle: 'No attribute bonuses',
			emptySub: 'This mod grants no stats.',
			newRow: (m) => ({
				attributeId: firstFree(
					m.attributes.map((a) => a.attributeId),
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
		tagsSection<IItemMod>()
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: (m) => ({ ...m, attributes: [], tags: [] }),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditItemMods', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					const changes = attributeChanges(record.attributes, baseline?.attributes, 'amount');
					if (changes.length) {
						await ApiRequest.post('AdminTools/AddEditItemModAttributes', { id, changes });
					}
				},
				async (id, record, baseline) => {
					if (childChanged(record.tags, baseline?.tags)) {
						await ApiRequest.post('AdminTools/SetTagsForItemMod', { id, tagIds: record.tags });
					}
				}
			]
		})
};
