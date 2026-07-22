import { ApiRequest, type ITag } from '$lib/api';
import { reference } from '../reference.svelte';
import { persistEntity } from '../save-helpers';
import type { EntityConfig } from './types';

const refresh = async (): Promise<ITag[]> => {
	const tags = await ApiRequest.get('Tags');
	reference.tags = tags;
	return tags;
};

export const tagEntity: EntityConfig<ITag> = {
	key: 'tags',
	label: 'Tags',
	singular: 'Tag',
	glyph: 'tag',
	blankName: 'Unnamed tag',
	newItem: (id) => ({ id, name: '', tagCategoryId: reference.tagCategories[0]?.id ?? 1 }),
	listBadge: (t) => reference.tagCategories.find((c) => c.id === t.tagCategoryId)?.name ?? null,
	badgeColor: (t) => reference.tagColor(t.tagCategoryId).fg,
	meta: (t) => {
		const usage = reference.tagUsage(t.id);
		return [
			['item', usage.items],
			['mod', usage.mods]
		];
	},
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name & category',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Tag Name',
					type: 'text',
					placeholder: 'Name this tag…',
					grow: true,
					required: true,
					reqMsg: 'Missing name',
					maxLength: 50
				},
				{ key: 'tagCategoryId', label: 'Category', type: 'select', options: reference.tagCategoryOptions, width: 200 }
			]
		},
		{
			key: 'usage',
			label: 'Usage',
			glyph: 'box',
			desc: 'Where this tag is applied (read-only)',
			count: (t) => {
				const usage = reference.tagUsage(t.id);
				return usage.items + usage.mods;
			},
			kind: 'usage'
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: (t) => ({ ...t }),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditTags', changes),
			refresh
		})
};
