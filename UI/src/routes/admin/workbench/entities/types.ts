import type { WorkbenchIconKind } from '../WorkbenchIcon.svelte';

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

export interface FieldConfig<T> {
	key: keyof T & string;
	label: string;
	type: 'text' | 'number' | 'toggle' | 'textarea' | 'select';
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
	/** Select option provider (resolved at render so it can read live ref data). */
	options?: () => SelectOption[];
	/** Marks the field required; an empty value raises the field/section/record warning. */
	required?: boolean;
	reqMsg?: string;
}

export interface ColumnConfig {
	key: string;
	label: string;
	type: 'select' | 'number' | 'share';
	options?: () => SelectOption[];
	align?: 'r';
	width?: number;
	min?: number;
	/** Select columns: disable options already chosen in sibling rows. */
	unique?: boolean;
	allowNegative?: boolean;
	/** Share columns: which numeric field drives the bar (default "weight"). */
	weightKey?: string;
	/**
	 * Share columns: override the denominator. Defaults to the sum across sibling
	 * rows; an enemy's spawn share instead competes against all enemies in the zone.
	 */
	shareTotal?: (row: Record<string, number>, rows: Record<string, number>[], record: unknown) => number;
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

export interface TableSectionConfig<T> extends BaseSection<T> {
	kind: 'table';
	itemsKey: keyof T & string;
	addLabel: string;
	emptyIcon: WorkbenchIconKind;
	emptyTitle: string;
	emptySub: string;
	newRow: (rec: T) => Record<string, number>;
	columns: ColumnConfig[];
}

export interface ChipsSectionConfig<T> extends BaseSection<T> {
	kind: 'chips';
	itemsKey: keyof T & string;
	catalogue: () => { id: number; name: string }[];
	labelOf: (entry: { id: number; name: string }) => string;
	metaOf: (entry: { id: number; name: string }) => string;
	emptyIcon: WorkbenchIconKind;
	emptyTitle: string;
	emptySub: string;
	addLabel: string;
}

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
	glyph: WorkbenchIconKind;
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
