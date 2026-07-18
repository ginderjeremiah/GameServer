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
}): TableSectionConfig<T> => {
	const rowsOf = (rec: T) => fieldsOf(rec)[config.itemsKey] as AttributeDistributionRow[];
	return {
		key: config.key,
		label: 'Attributes',
		glyph: 'bars',
		desc: config.desc,
		count: (rec) => rowsOf(rec).length,
		warn: (rec) => (rowsOf(rec).length ? null : 'No attribute distribution'),
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
				reference.attributeOptions()
			),
			baseAmount: 0,
			amountPerLevel: 0
		}),
		columns: [
			{
				key: 'attributeId',
				label: 'Attribute',
				type: 'attribute',
				options: reference.attributeOptions,
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
