import type { AdminGlyphKind } from '../../AdminGlyph.svelte';
import type { WorkbenchIconKind } from '../WorkbenchIcon.svelte';

/**
 * An entity's headline glyph is drawn by both the sidebar ({@link AdminGlyphKind}) and the
 * detail header ({@link WorkbenchIconKind}), so it must be a member of both glyph sets.
 */
export type EntityGlyphKind = Extract<WorkbenchIconKind, AdminGlyphKind>;

/** Every workbench record is keyed by a numeric id (negative while unsaved). */
export interface Identified {
	id: number;
	name?: string;
	/**
	 * Retire timestamp for reference entities (ISO string when retired, null/absent while active).
	 * Present only on {@link EntityConfig.retireable} entities; tags and never-saved records leave it unset.
	 */
	retiredAt?: string | null;
}

/**
 * Read/write a record's fields by config-declared string key. Workbench
 * components are entity-agnostic, so they access fields dynamically; this
 * bridges the opaque {@link Identified} to an indexable view without leaking
 * `any` to call sites.
 */
export const fieldsOf = (record: object): Record<string, unknown> => record as unknown as Record<string, unknown>;

export interface SelectOption {
	value: number;
	text: string;
}

/** A single toggleable bit in a {@link FieldConfig} `flags` field (a `[Flags]` enum member). */
export interface FlagOption {
	label: string;
	/** The enum member's bit value (e.g. `ESkillAcquisition.Player`). */
	value: number;
}

export interface FieldConfig<T> {
	key: keyof T & string;
	label: string;
	/** `attribute` renders the searchable, group-by-type AttributePicker over the same `options`
	 *  provider as `select` — used for every EAttribute picker now the enum is large (#1327). */
	type: 'text' | 'number' | 'toggle' | 'textarea' | 'select' | 'attribute' | 'flags';
	placeholder?: string;
	/** Lets a text/textarea field stretch to fill the row. */
	grow?: boolean;
	/** Fixed width (px) for number/select fields. */
	width?: number;
	/** Unit shown inside a number input (e.g. "ms", "lv"). */
	suffix?: string;
	allowNegative?: boolean;
	/** Toggle labels. */
	onLabel?: string;
	offLabel?: string;
	/**
	 * Select option provider (resolved at render so it can read live ref data). Receives the
	 * field's current value so a retireable provider can keep an already-authored (now retired)
	 * reference visible rather than blanking it.
	 */
	options?: (current?: number) => SelectOption[];
	/** The toggleable bits for a `flags` field (each renders as a checkbox over a `[Flags]` bitmask). */
	flags?: FlagOption[];
	/** Marks the field required; an empty value raises the field/section/record warning. */
	required?: boolean;
	reqMsg?: string;
}

export interface ColumnConfig {
	key: string;
	label: string;
	/** `attribute` renders the searchable, group-by-type AttributePicker (#1327); `text` a free-form
	 *  string input (e.g. tutorial tour step copy); otherwise as `select`. */
	type: 'select' | 'attribute' | 'number' | 'share' | 'text';
	/**
	 * Select option provider; receives the row's current value (so a retired reference stays visible)
	 * and the full row (so options can be filtered by a sibling column, e.g. an item picker narrowed to
	 * the row's equipment-slot category).
	 */
	options?: (current?: number, row?: Record<string, number | string>) => SelectOption[];
	align?: 'r';
	width?: number;
	min?: number;
	placeholder?: string;
	/** Select columns: disable options already chosen in sibling rows. */
	unique?: boolean;
	allowNegative?: boolean;
	/** Share columns: which numeric field drives the bar (default "weight"). */
	weightKey?: string;
	/**
	 * Share columns: override the denominator. Defaults to the sum across sibling
	 * rows; an enemy's spawn share instead competes against all enemies in the zone.
	 */
	shareTotal?: (
		row: Record<string, number | string>,
		rows: Record<string, number | string>[],
		record: unknown
	) => number;
}

interface BaseSection<T> {
	key: string;
	label: string;
	glyph: WorkbenchIconKind;
	desc?: string;
	/** Tab count badge. */
	count?: (rec: T) => number;
	/** Section-level validation warning (e.g. "No skills assigned"). */
	warn?: (rec: T) => string | null;
	/**
	 * Record keys that constitute a change for this section's tab dirty-dot. Used by
	 * custom-kind sections (which have no single `fields`/`itemsKey` to diff); the
	 * built-in kinds derive dirtiness from their fields/collection instead.
	 */
	dirtyKeys?: (keyof T & string)[];
}

export interface FieldsSectionConfig<T> extends BaseSection<T> {
	kind: 'fields';
	fields: FieldConfig<T>[];
}

/** An opt-in bulk action rendered beside a {@link TableSectionConfig}'s Add button (e.g. "Even split"
 *  equalizing a portion table's weights). `apply` mutates the section's rows in place inside the store
 *  patch, so the change flows through the same dirty-tracking as a cell edit. */
export interface TableActionConfig {
	label: string;
	glyph?: WorkbenchIconKind;
	apply: (rows: Record<string, number | string>[]) => void;
}

export interface TableSectionConfig<T> extends BaseSection<T> {
	kind: 'table';
	itemsKey: keyof T & string;
	/** Opt-in bulk actions rendered beside Add (shown only while the table has rows). */
	actions?: TableActionConfig[];
	/**
	 * The row field that uniquely identifies a row within the collection — `id` for surrogate-id
	 * collections (skill effects, item mod slots) and the natural key for attribute/relation tables
	 * (`attributeId`, `enemyId`, `zoneId`). Used to key the rendered rows and match them to their
	 * baseline by identity rather than array position, so a mid-list delete can't corrupt the
	 * dirty edges or reuse a row's input state.
	 */
	rowKey: string;
	addLabel: string;
	emptyIcon: WorkbenchIconKind;
	emptyTitle: string;
	emptySub: string;
	newRow: (rec: T) => Record<string, number | string>;
	columns: ColumnConfig[];
}

/**
 * The minimum a `chips` catalogue entry carries — what the renderer itself needs (id to key/match a
 * chip, name to fall back on, retired/addable to style and gate the add-list). A section may supply a
 * wider entry (see {@link ChipsSectionConfig}'s `E`) so `labelOf`/`metaOf` can read extra meta fields.
 */
export interface ChipCatalogueEntry {
	id: number;
	name: string;
	retired?: boolean;
	addable?: boolean;
}

export interface ChipsSectionConfig<T, E extends ChipCatalogueEntry = ChipCatalogueEntry> extends BaseSection<T> {
	kind: 'chips';
	itemsKey: keyof T & string;
	/**
	 * Catalogue of addable entries. A `retired` entry, or one with `addable: false` (e.g. a skill not
	 * flagged for this channel), stays available for display of an already-assigned chip but can't be
	 * newly added. `E` lets a section declare a richer entry shape that its `labelOf`/`metaOf` read.
	 */
	catalogue: () => E[];
	// Method shorthand (not arrow properties) so the entry parameter is compared bivariantly: a section
	// built on a wider `E` stays assignable to the base `ChipsSectionConfig<T>` the renderer consumes.
	labelOf(entry: E): string;
	metaOf(entry: E): string;
	emptyIcon: WorkbenchIconKind;
	emptyTitle: string;
	emptySub: string;
	addLabel: string;
}

/**
 * Builds a {@link ChipsSectionConfig} whose catalogue carries entity-specific meta (e.g. a skill's
 * `baseDamage`), inferring the entry type `E` from `catalogue` so `labelOf`/`metaOf` read those fields
 * type-safely — no casting back through `as unknown as`. Curried so the record type `T` is given
 * explicitly while `E` is inferred. The result erases `E` to the base entry for storage in the record's
 * section list; the renderer only needs the base contract.
 */
export const chipsSection =
	<T>() =>
	<E extends ChipCatalogueEntry>(section: ChipsSectionConfig<T, E>): ChipsSectionConfig<T> =>
		section;

export interface TagsSectionConfig<T> extends BaseSection<T> {
	kind: 'tags';
	itemsKey: keyof T & string;
}

export interface UsageSectionConfig<T> extends BaseSection<T> {
	kind: 'usage';
}

/**
 * The challenge condition builder: objective type + the read-only statistic/entity
 * derived from it + goal + scope. Carries no extra config — the editor reads the
 * challenge-type metadata from reference data.
 */
export interface ChallengeConditionSectionConfig<T> extends BaseSection<T> {
	kind: 'challenge-condition';
}

/** The challenge reward picker (item and/or mod) with hard cross-challenge exclusivity. */
export interface ChallengeRewardSectionConfig<T> extends BaseSection<T> {
	kind: 'challenge-reward';
}

export type SectionConfig<T> =
	| FieldsSectionConfig<T>
	| TableSectionConfig<T>
	| ChipsSectionConfig<T>
	| TagsSectionConfig<T>
	| UsageSectionConfig<T>
	| ChallengeConditionSectionConfig<T>
	| ChallengeRewardSectionConfig<T>;

/** The per-record diff handed to an entity's persist routine. */
export interface SaveDiff<T> {
	added: T[];
	modified: { record: T; baseline: T }[];
	deleted: T[];
	/** Ids that existed before this save — used to resolve the persisted ids of added records. */
	existingIds: number[];
}

export interface EntityConfig<T extends Identified> {
	key: string;
	label: string;
	singular: string;
	glyph: EntityGlyphKind;
	blankName: string;
	/**
	 * Zero-based-id reference entity: records are <em>retired</em> (kept at their slot, resolvable by id)
	 * rather than hard-deleted, which would open an id gap that mis-resolves index lookups. The detail
	 * header offers Retire/Reinstate instead of Delete. Omitted (delete-able) for non-index entities like tags.
	 */
	retireable?: boolean;
	newItem: (id: number) => T;
	listBadge?: (rec: T) => string | null;
	badgeColor?: (rec: T) => string;
	/**
	 * Derives the list/detail title for an entity that has no editable `name` of its own — a recipe is
	 * identified by its result skill, not a stored name. Resolved live (so it tracks an in-flight edit of
	 * the field it derives from) and also drives list search. Falls back to `name` / {@link blankName}
	 * when omitted or when it returns an empty string.
	 */
	title?: (rec: T) => string;
	/** Optional one-line preview rendered under the detail title (e.g. a challenge's objective). */
	headline?: (rec: T) => string;
	/** Compact stat line for the list row: [label, value][] (blank label = bare value). */
	meta: (rec: T) => [string, string | number][];
	sections: SectionConfig<T>[];
	/** Fetch the saved record list (with caches refreshed). */
	refresh: () => Promise<T[]>;
	/** Persist a diff against existing/new endpoints and return the fresh saved list. */
	persist: (diff: SaveDiff<T>) => Promise<T[]>;
}
