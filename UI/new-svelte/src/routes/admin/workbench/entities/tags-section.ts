import type { TagsSectionConfig } from './types';

/**
 * Shared Tags section — identical for Items and Item Mods (the two old
 * SetItemTags / SetItemModTags tools), proving the tag UX generalizes.
 */
export const tagsSection = <T extends { tags: number[] }>(): TagsSectionConfig<T> => ({
	key: 'tags',
	label: 'Tags',
	glyph: 'tag',
	desc: 'Categorized tags applied to this record',
	count: (rec) => rec.tags.length,
	kind: 'tags',
	itemsKey: 'tags' as keyof T & string
});
