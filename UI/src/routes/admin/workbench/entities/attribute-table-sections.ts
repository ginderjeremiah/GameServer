import { attributeName } from '$lib/common';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { firstFree } from './helpers';
import { fieldsOf, type Identified, type TableSectionConfig } from './types';

interface AttributeDistributionRow {
	attributeId: number;
	baseAmount: number;
	amountPerLevel: number;
}

interface AttributeBonusRow {
	attributeId: number;
	amount: number;
}

/**
 * Shared "stat distribution per level" table — identical shape for Enemies and Classes
 * (attributeId + baseAmount + amountPerLevel, one row per attribute); only the collection
 * key, section key, description and empty-state copy vary per entity.
 */
export const attributeDistributionSection = <T extends Identified>(config: {
	key: string;
	itemsKey: keyof T & string;
	desc: string;
	emptySub: string;
	/**
	 * Restricts which attributes are selectable/valid in this table — e.g. a class distribution is
	 * core-attribute-only (`AdminClasses.FindAttributeDistributionViolation`), while an enemy's stays
	 * unrestricted. Omit for no restriction.
	 */
	attributeFilter?: (attributeId: number) => boolean;
}): TableSectionConfig<T> => {
	const { attributeFilter } = config;
	const rowsOf = (rec: T) => fieldsOf(rec)[config.itemsKey] as AttributeDistributionRow[];
	const attributeOptions = () =>
		attributeFilter
			? reference.attributeOptions().filter((o) => attributeFilter(o.value))
			: reference.attributeOptions();
	return {
		key: config.key,
		label: 'Attributes',
		glyph: 'bars',
		desc: config.desc,
		count: (rec) => rowsOf(rec).length,
		warn: (rec) => {
			const rows = rowsOf(rec);
			if (!rows.length) {
				return 'No attribute distribution';
			}
			// A row can violate the restriction even though the picker excludes it from new authoring —
			// e.g. it was set before the restriction existed. Hard-rejected on save, so it blocks (#2376).
			const violator = attributeFilter && rows.find((r) => !attributeFilter(r.attributeId));
			if (violator) {
				return {
					message: `'${attributeName(violator.attributeId, staticData.attributes)}' is not a core attribute and cannot have a distribution here`,
					blocking: true
				};
			}
			return null;
		},
		kind: 'table',
		itemsKey: config.itemsKey,
		rowKey: 'attributeId',
		addLabel: 'Add attribute',
		emptyIcon: 'bars',
		emptyTitle: 'No attributes set',
		emptySub: config.emptySub,
		newRow: (rec) => ({
			attributeId: firstFree(
				rowsOf(rec).map((a) => a.attributeId),
				attributeOptions()
			),
			baseAmount: 0,
			amountPerLevel: 0
		}),
		columns: [
			{
				key: 'attributeId',
				label: 'Attribute',
				type: 'attribute',
				options: attributeOptions,
				min: 190,
				unique: true
			},
			{ key: 'baseAmount', label: 'Base', type: 'number', align: 'r', width: 110, allowNegative: true },
			{ key: 'amountPerLevel', label: 'Per Level', type: 'number', align: 'r', width: 110, allowNegative: true }
		]
	};
};

/**
 * Shared "flat stat bonus" table — identical shape for Items and Item Mods (attributeId +
 * amount, one row per attribute); only the collection key and empty-state copy vary.
 */
export const attributeBonusSection = <T extends Identified>(config: {
	itemsKey: keyof T & string;
	emptySub: string;
}): TableSectionConfig<T> => {
	const rowsOf = (rec: T) => fieldsOf(rec)[config.itemsKey] as AttributeBonusRow[];
	return {
		key: 'attributes',
		label: 'Attributes',
		glyph: 'bars',
		desc: 'Flat stat bonuses granted',
		count: (rec) => rowsOf(rec).length,
		warn: (rec) => (rowsOf(rec).length ? null : 'No attributes'),
		kind: 'table',
		itemsKey: config.itemsKey,
		rowKey: 'attributeId',
		addLabel: 'Add bonus',
		emptyIcon: 'bars',
		emptyTitle: 'No attribute bonuses',
		emptySub: config.emptySub,
		newRow: (rec) => ({
			attributeId: firstFree(
				rowsOf(rec).map((a) => a.attributeId),
				reference.attributeOptions()
			),
			amount: 1
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
			{ key: 'amount', label: 'Amount', type: 'number', align: 'r', width: 120, allowNegative: true }
		]
	};
};
