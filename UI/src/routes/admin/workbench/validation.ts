import type { EntityConfig, FieldConfig, Identified, SectionConfig, Warning } from './entities/types';

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

const resolveWarning = <T>(section: SectionConfig<T>, rec: T, baseline?: T): Warning | null => {
	const result = section.warn?.(rec, baseline) ?? null;
	return result === null ? null : typeof result === 'string' ? { message: result } : result;
};

export function sectionWarnings<T>(section: SectionConfig<T>, rec: T, baseline?: T): string[] {
	const warning = resolveWarning(section, rec, baseline);
	if (section.kind === 'fields') {
		// A fields section warns on each failing required field, plus an optional section-level `warn`
		// for a cross-field rule the per-field `required` check can't express (e.g. a recipe result that
		// resolves to a non-Synthesis or retired skill).
		const fieldWarnings = section.fields.map((f) => fieldWarn(f, rec)).filter((w): w is string => !!w);
		return warning ? [...fieldWarnings, warning.message] : fieldWarnings;
	}
	return warning ? [warning.message] : [];
}

/** The section's warning message when it's flagged {@link Warning.blocking}, else null — used to gate
 *  Save separately from the advisory triangle/list display, which shows every warning regardless. */
export function sectionBlockingWarning<T>(section: SectionConfig<T>, rec: T, baseline?: T): string | null {
	const warning = resolveWarning(section, rec, baseline);
	return warning?.blocking ? warning.message : null;
}

export function entityWarnings<T extends Identified>(entity: EntityConfig<T>, rec: T, baseline?: T): string[] {
	return entity.sections.flatMap((s) => sectionWarnings(s, rec, baseline));
}

export function entityBlockingWarnings<T extends Identified>(entity: EntityConfig<T>, rec: T, baseline?: T): string[] {
	return entity.sections.map((s) => sectionBlockingWarning(s, rec, baseline)).filter((w): w is string => !!w);
}
