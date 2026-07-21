import { ApiRequest, EItemModType, ERarity, fetchSocketData, type IItemMod } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { attributeChanges, childChanged, guardedSave, persistEntity } from '../save-helpers';
import { attributeBonusSection } from './attribute-table-sections';
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
	retireable: true,
	newItem: (id) => ({
		id,
		name: '',
		description: '',
		itemModTypeId: EItemModType.Component,
		rarityId: ERarity.Common,
		attributes: [],
		designerNotes: '',
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
					reqMsg: 'No description',
					maxLength: 500
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this mod exists — authoring notes (never shown to players)…',
					grow: true
				}
			]
		},
		attributeBonusSection<IItemMod>({
			itemsKey: 'attributes',
			emptySub: 'This mod grants no stats.'
		}),
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
					return guardedSave(changes.length > 0, () =>
						ApiRequest.post('AdminTools/AddEditItemModAttributes', { id, changes })
					);
				},
				async (id, record, baseline) =>
					guardedSave(childChanged(record.tags, baseline?.tags), () =>
						ApiRequest.post('AdminTools/SetTagsForItemMod', { id, tagIds: record.tags })
					)
			]
		})
};
