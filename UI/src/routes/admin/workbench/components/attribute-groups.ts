import { EAttributeType, type EDamageTypeKey, type IAttribute } from '$lib/api';
import { damageTypeKeyName } from '$lib/common';
import type { SelectOption } from '../entities/types';

/*
 * Grouping logic for the searchable attribute picker (#1327, spike #1320 Area E). The flat enum has
 * grown past what a single dropdown handles, so the picker groups options by the reference set's
 * display taxonomy (Primary / Secondary / Status), with the damage-type amplification/resistance
 * family (Affinity) further split into one sub-group per damage-type key — mirroring how the
 * attribute-breakdown screen groups the same set. Kept pure (operating on plain options + the
 * reference set) so the grouping and search filtering are unit-testable without the live stores.
 */

/** A group of attribute options for the picker: a taxonomy band (Primary / Secondary / Status) or
 *  one damage-type sub-group of the Affinity family. */
export interface AttributeOptionGroup {
	/** Stable render key — `none`, `type-<EAttributeType>`, or `affinity-<EDamageTypeKey>`. */
	key: string;
	/** Header label. Empty (`''`) renders no header — used for the leading sentinel options (e.g. "None"). */
	label: string;
	/** Set for a damage-type Affinity sub-group, so the header can tint/icon by its type. */
	damageTypeKey?: EDamageTypeKey;
	options: SelectOption[];
}

/** Taxonomy bands in render order; Affinity is split into per-damage-type sub-groups. */
const TYPE_BANDS: { type: EAttributeType; label: string }[] = [
	{ type: EAttributeType.Primary, label: 'Primary' },
	{ type: EAttributeType.Secondary, label: 'Secondary' },
	{ type: EAttributeType.Status, label: 'Status' },
	{ type: EAttributeType.Affinity, label: 'Affinity' }
];

/** Splits the Affinity options into one sub-group per distinct damage-type key (in key order), each
 *  labelled by its type. An Affinity attribute with no `damageTypeKey` (none today) falls back to a
 *  single untyped "Affinity" group rather than being dropped. */
function affinitySubgroups(options: SelectOption[], byId: Map<number, IAttribute>): AttributeOptionGroup[] {
	const keyed = options.map((o) => ({ o, key: byId.get(o.value)?.damageTypeKey }));
	const distinctKeys = keyed
		.map((k) => k.key)
		.filter((key): key is EDamageTypeKey => key !== undefined)
		.filter((key, i, all) => all.indexOf(key) === i)
		.sort((a, b) => a - b);
	const groups: AttributeOptionGroup[] = distinctKeys.map((key) => ({
		key: `affinity-${key}`,
		label: damageTypeKeyName(key),
		damageTypeKey: key,
		options: keyed.filter((k) => k.key === key).map((k) => k.o)
	}));
	const untyped = keyed.filter((k) => k.key === undefined).map((k) => k.o);
	if (untyped.length > 0) {
		groups.push({ key: 'affinity', label: 'Affinity', options: untyped });
	}
	return groups;
}

/**
 * Groups picker options by the reference set's display taxonomy, each sorted by the set's canonical
 * `displayOrder`, dropping empty bands. The Affinity family is further split by damage type. Options
 * that don't resolve to a real attribute (the optional "None" sentinel, value `-1`) lead in a single
 * headerless group. When the reference set is unavailable, falls back to one flat, headerless group so
 * the picker still renders (and stays searchable).
 */
export function groupAttributeOptions(
	options: SelectOption[],
	attributes: IAttribute[] | undefined
): AttributeOptionGroup[] {
	if (!attributes || attributes.length === 0) {
		return options.length > 0 ? [{ key: 'all', label: '', options }] : [];
	}
	const byId = new Map(attributes.map((a) => [a.id, a]));
	const order = (o: SelectOption) => byId.get(o.value)?.displayOrder ?? o.value;

	const groups: AttributeOptionGroup[] = [];
	const sentinels = options.filter((o) => !byId.has(o.value));
	if (sentinels.length > 0) {
		groups.push({ key: 'none', label: '', options: sentinels });
	}
	for (const band of TYPE_BANDS) {
		const inBand = options
			.filter((o) => byId.get(o.value)?.attributeType === band.type)
			.sort((a, b) => order(a) - order(b));
		if (inBand.length === 0) {
			continue;
		}
		if (band.type === EAttributeType.Affinity) {
			groups.push(...affinitySubgroups(inBand, byId));
		} else {
			groups.push({ key: `type-${band.type}`, label: band.label, options: inBand });
		}
	}
	return groups;
}

/** Case-insensitive filter of grouped options by their display text, dropping now-empty groups. */
export function filterAttributeGroups(groups: AttributeOptionGroup[], query: string): AttributeOptionGroup[] {
	const q = query.trim().toLowerCase();
	if (q === '') {
		return groups;
	}
	return groups
		.map((g) => ({ ...g, options: g.options.filter((o) => o.text.toLowerCase().includes(q)) }))
		.filter((g) => g.options.length > 0);
}
