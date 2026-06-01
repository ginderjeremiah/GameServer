import type { EntityConfig, FieldConfig, Identified, SectionConfig } from './entities/types';

/**
 * Validation is declared where it lives — a field is `required` (with its own
 * message) or a non-field section carries a `warn` predicate. The record-level
 * list triangle, the per-tab triangle, and the offending field all read from
 * these helpers, so a warning can never show on a record without also pointing
 * at the exact tab and field that caused it.
 */
export function fieldWarn<T>(field: FieldConfig<T>, rec: T): string | null {
	if (!field.required) {
		return null;
	}
	const value = rec[field.key];
	const empty = value === null || value === undefined || String(value).trim() === '';
	return empty ? (field.reqMsg ?? `${field.label} required`) : null;
}

export function sectionWarnings<T>(section: SectionConfig<T>, rec: T): string[] {
	if (section.kind === 'fields') {
		return section.fields.map((f) => fieldWarn(f, rec)).filter((w): w is string => !!w);
	}
	if (section.warn) {
		const message = section.warn(rec);
		return message ? [message] : [];
	}
	return [];
}

export function entityWarnings<T extends Identified>(entity: EntityConfig<T>, rec: T): string[] {
	return entity.sections.flatMap((s) => sectionWarnings(s, rec));
}
