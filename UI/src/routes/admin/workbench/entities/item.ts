import { ApiRequest, EItemCategory, ERarity, fetchSocketData, type IItem } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { attributeChanges, childChanged, guardedSave, modSlotChanges, persistEntity } from '../save-helpers';
import { attributeBonusSection } from './attribute-table-sections';
import { tagsSection } from './tags-section';
import type { EntityConfig } from './types';

/**
 * An item with its optional weapon type widened to a plain number so the select can use a "None" sentinel
 * (-1) — mirroring how it (and classes) handle their other optional enum/FK fields.
 */
export interface WorkbenchItem extends Omit<IItem, 'weaponType'> {
	weaponType: number;
}

const refresh = async (): Promise<WorkbenchItem[]> => {
	const items = await fetchSocketData('GetItems');
	staticData.items = items;
	// Normalise the optional FK / weapon-type fields to the select's "None" sentinel (-1) for the editable
	// copy; staticData.items stays raw (null = none) for the game/other screens.
	return items.map((item) => ({
		...item,
		grantedSkillId: item.grantedSkillId ?? -1,
		requiredProficiencyId: item.requiredProficiencyId ?? -1,
		weaponType: item.weaponType ?? -1
	}));
};

export const itemEntity: EntityConfig<WorkbenchItem> = {
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
		weaponType: -1,
		requiredProficiencyId: -1,
		requiredProficiencyLevel: 1,
		designerNotes: '',
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
			// The no-stranding invariant, hard-rejected by AdminItems on save, so every branch blocks (#2217):
			// a weapon must declare a weapon type and a granted signature skill; only a weapon may carry one.
			warn: (it) => {
				if (it.itemCategoryId === EItemCategory.Weapon) {
					if (it.weaponType === -1) {
						return { message: 'Weapon needs a weapon type', blocking: true };
					}
					if (it.grantedSkillId === -1) {
						return { message: 'Weapon needs a granted skill', blocking: true };
					}
					return null;
				}
				return it.weaponType === -1 ? null : { message: 'Only weapons have a weapon type', blocking: true };
			},
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
					reqMsg: 'No icon path',
					maxLength: 50
				},
				{
					key: 'grantedSkillId',
					label: 'Granted Skill',
					type: 'select',
					options: reference.grantedSkillOptions,
					width: 240
				},
				{
					key: 'weaponType',
					label: 'Weapon Type',
					type: 'select',
					options: reference.weaponTypeOptions,
					width: 170
				},
				{
					key: 'requiredProficiencyId',
					label: 'Required Proficiency',
					type: 'select',
					options: reference.requiredProficiencyOptions,
					width: 240
				},
				{ key: 'requiredProficiencyLevel', label: 'Required Level', type: 'number', width: 150 },
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Flavor text…',
					grow: true,
					maxLength: 500
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this item exists — authoring notes (never shown to players)…',
					grow: true
				}
			]
		},
		attributeBonusSection<WorkbenchItem>({
			itemsKey: 'attributes',
			emptySub: 'This item grants no stats.'
		}),
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
		tagsSection<WorkbenchItem>()
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
				// Map the "None" sentinel (-1) back to no weapon type (only meaningful on a weapon).
				weaponType: it.weaponType === -1 ? undefined : it.weaponType,
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
					return guardedSave(changes.length > 0, () =>
						ApiRequest.post('AdminTools/AddEditItemAttributes', { id, changes })
					);
				},
				async (id, record, baseline) => {
					const changes = modSlotChanges(record.modSlots, baseline?.modSlots, id);
					return guardedSave(changes.length > 0, () => ApiRequest.post('AdminTools/AddEditItemModSlots', changes));
				},
				async (id, record, baseline) =>
					guardedSave(childChanged(record.tags, baseline?.tags), () =>
						ApiRequest.post('AdminTools/SetTagsForItem', { id, tagIds: record.tags })
					)
			]
		})
};
