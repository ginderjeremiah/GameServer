import { EChangeType, type IChange, type IItemModSlot, type ISkillDamagePortion, type ISkillEffect } from '$lib/api';
import type { Identified, SaveDiff } from './entities/types';

/**
 * Recursively normalise a value to a canonical shape: object keys sorted, and `null`/`undefined`/
 * absent keys collapsed to the same (omitted) state. Lets structural comparison ignore key order
 * and treat a server `null` for an unset optional as equal to a locally-absent key — both of which
 * a raw `JSON.stringify` compare would otherwise read as a (phantom) difference.
 */
const canonicalize = (value: unknown): unknown => {
	if (value === null || value === undefined) {
		return null;
	}
	if (Array.isArray(value)) {
		return value.map(canonicalize);
	}
	if (typeof value === 'object') {
		const out: Record<string, unknown> = {};
		for (const key of Object.keys(value as Record<string, unknown>).sort()) {
			const normalized = canonicalize((value as Record<string, unknown>)[key]);
			if (normalized !== null) {
				out[key] = normalized;
			}
		}
		return out;
	}
	return value;
};

/** Structural equality that is insensitive to object key order and to `null`-vs-absent optionals. */
export const canonicalEqual = (a: unknown, b: unknown): boolean =>
	JSON.stringify(canonicalize(a)) === JSON.stringify(canonicalize(b));

export const listsEqual = canonicalEqual;

/** True when a child collection differs from its saved baseline (added records have none). */
export const childChanged = <T>(current: T, baseline: T | undefined): boolean =>
	baseline === undefined || !listsEqual(current, baseline);

/**
 * Map the local (negative) ids of added records to their persisted ids. The backend appends adds in
 * send order, so the k-th added record maps to the k-th lowest id absent from the pre-save set.
 */
export const resolveNewIds = (
	fresh: { id: number }[],
	existingIds: Iterable<number>,
	added: { id: number }[]
): Map<number, number> => {
	const existing = new Set(existingIds);
	const newlyPersisted = fresh.filter((record) => !existing.has(record.id)).sort((a, b) => a.id - b.id);
	const idFor = new Map<number, number>();
	added.forEach((record, index) => {
		const persisted = newlyPersisted[index];
		if (persisted) {
			idFor.set(record.id, persisted.id);
		}
	});
	return idFor;
};

/** Resolve a possibly-local id through the new-id map (returns the input when already persisted). */
export const resolveId = (id: number, idMap: Map<number, number>): number => idMap.get(id) ?? id;

/** Returns whether it actually wrote — a no-op (unchanged collection) must report `false`. */
type ChildSaver<T> = (id: number, record: T, baseline: T | undefined) => Promise<boolean>;

interface PersistOptions<T extends Identified, D> {
	diff: SaveDiff<T>;
	/** Maps a record to the DTO sent through the primary Add/Edit/Delete endpoint. */
	toPrimaryDto: (record: T) => D;
	/** Posts the assembled primary change set (one HTTP call). */
	postPrimary: (changes: IChange<D>[]) => Promise<unknown>;
	/** Refetches the saved record list (caches refreshed). */
	refresh: () => Promise<T[]>;
	/** Per child-section persisters, run for every added/modified record with its resolved id. */
	childSavers?: ChildSaver<T>[];
}

/**
 * Thrown by {@link persistEntity} when the primary Add/Edit/Delete batch (or an earlier child
 * saver) already committed before a later step failed. The server is then ahead of the caller's
 * baseline, so the caller must re-seed from server truth — otherwise a retry would re-Add the
 * already-persisted records. A *pre-commit* failure (e.g. `postPrimary` itself throwing before any
 * write lands) committed nothing and propagates the raw error instead, so the caller can keep the
 * user's unsaved edits for a clean retry.
 */
export class PersistFailedError extends Error {
	constructor(cause: unknown) {
		super(cause instanceof Error ? cause.message : 'Failed to save changes.', { cause });
		this.name = 'PersistFailedError';
	}
}

/**
 * Generic per-entity save: persists identity-level Add/Edit/Delete in one call,
 * refetches to resolve the ids of newly-added records (positionally — the backend
 * appends adds in send order), then runs each child-section persister for every
 * added/modified record against its real id. Returns the final saved list.
 *
 * A failure once anything has committed is rethrown as {@link PersistFailedError}; a pre-commit
 * failure propagates raw (see that type's docs).
 */
export async function persistEntity<T extends Identified, D>(opts: PersistOptions<T, D>): Promise<T[]> {
	const { diff, toPrimaryDto, postPrimary, refresh, childSavers = [] } = opts;

	// A record can be "modified" because only a child collection changed (e.g. a skill
	// whose damage multipliers were edited but whose identity fields weren't). Only send
	// an identity-level Edit when the primary DTO itself differs, so a child-only edit
	// never needlessly hits — and re-persists — the Add/Edit endpoint. Child collections
	// are handled by the child savers below, which already skip untouched relationships.
	const identityEdits = diff.modified.filter(
		({ record, baseline }) => !listsEqual(toPrimaryDto(record), toPrimaryDto(baseline))
	);

	const changes: IChange<D>[] = [
		...diff.added.map((record) => ({ changeType: EChangeType.Add, item: toPrimaryDto(record) })),
		...identityEdits.map(({ record }) => ({ changeType: EChangeType.Edit, item: toPrimaryDto(record) })),
		...diff.deleted.map((record) => ({ changeType: EChangeType.Delete, item: toPrimaryDto(record) }))
	];

	// `committed` flips the moment a write has definitely landed; it gates whether a later failure
	// is recoverable-by-keeping-edits (pre-commit) or requires the caller to re-seed (post-commit).
	let committed = false;
	try {
		if (changes.length) {
			await postPrimary(changes);
			committed = true;
		}

		const fresh = await refresh();

		// Records present after save but absent before are the persisted adds.
		const idFor = resolveNewIds(fresh, diff.existingIds, diff.added);

		const childTargets: { id: number; record: T; baseline: T | undefined }[] = [
			...diff.added.map((record) => ({ id: idFor.get(record.id) ?? record.id, record, baseline: undefined })),
			...diff.modified.map(({ record, baseline }) => ({ id: record.id, record, baseline }))
		];

		if (childSavers.length && childTargets.length) {
			for (const target of childTargets) {
				for (const saver of childSavers) {
					const wrote = await saver(target.id, target.record, target.baseline);
					committed ||= wrote;
				}
			}
			return await refresh();
		}

		return fresh;
	} catch (ex) {
		if (committed) {
			throw new PersistFailedError(ex);
		}
		throw ex;
	}
}

interface AttributeRow {
	attributeId: number;
}

/**
 * Diffs an attribute-keyed collection (skill multipliers, item/mod attributes)
 * into Add/Edit/Delete changes of `{ attributeId, amount }`, reading the numeric
 * value from `valueKey` (e.g. "amount" or "multiplier").
 */
export function attributeChanges<K extends string, T extends AttributeRow & Record<K, number>>(
	current: T[],
	baseline: T[] | undefined,
	valueKey: K
): IChange<{ attributeId: number; amount: number }>[] {
	const base = baseline ?? [];
	const baseById = new Map(base.map((row) => [row.attributeId, row]));
	const currentIds = new Set(current.map((row) => row.attributeId));
	const changes: IChange<{ attributeId: number; amount: number }>[] = [];

	for (const row of current) {
		const previous = baseById.get(row.attributeId);
		if (!previous) {
			changes.push({ changeType: EChangeType.Add, item: { attributeId: row.attributeId, amount: row[valueKey] } });
		} else if (previous[valueKey] !== row[valueKey]) {
			changes.push({ changeType: EChangeType.Edit, item: { attributeId: row.attributeId, amount: row[valueKey] } });
		}
	}
	for (const row of base) {
		if (!currentIds.has(row.attributeId)) {
			changes.push({ changeType: EChangeType.Delete, item: { attributeId: row.attributeId, amount: row[valueKey] } });
		}
	}
	return changes;
}

/**
 * Diffs a skill's damage portions (keyed by leaf type, like {@link attributeChanges} is keyed by
 * attribute) into Add/Edit/Delete changes of `{ type, weight }`.
 */
export function damagePortionChanges(
	current: ISkillDamagePortion[],
	baseline: ISkillDamagePortion[] | undefined
): IChange<ISkillDamagePortion>[] {
	const base = baseline ?? [];
	const baseByType = new Map(base.map((row) => [row.type, row]));
	const currentTypes = new Set(current.map((row) => row.type));
	const changes: IChange<ISkillDamagePortion>[] = [];

	for (const row of current) {
		const previous = baseByType.get(row.type);
		if (!previous) {
			changes.push({ changeType: EChangeType.Add, item: { type: row.type, weight: row.weight } });
		} else if (previous.weight !== row.weight) {
			changes.push({ changeType: EChangeType.Edit, item: { type: row.type, weight: row.weight } });
		}
	}
	for (const row of base) {
		if (!currentTypes.has(row.type)) {
			changes.push({ changeType: EChangeType.Delete, item: { type: row.type, weight: row.weight } });
		}
	}
	return changes;
}

interface SurrogateId {
	id: number;
}

/**
 * Diffs a surrogate-id child collection into Add/Edit/Delete changes: a row with
 * `id <= 0` is a new, unsaved record (Add), an existing row whose fields differ
 * from its baseline is an Edit (per `equals`), and a baseline row absent from the
 * current set is a Delete. `toItem` maps a row to the persisted change payload
 * (default: the row itself); it lets a caller normalise the new-record id or inject
 * a parent key. Shared by every surrogate-id child collection (skill effects, item
 * mod slots, …).
 */
function surrogateIdChanges<T extends SurrogateId>(
	current: T[],
	baseline: T[] | undefined,
	equals: (a: T, b: T) => boolean,
	toItem: (row: T) => T = (row) => row
): IChange<T>[] {
	const base = baseline ?? [];
	const baseById = new Map(base.filter((row) => row.id > 0).map((row) => [row.id, row]));
	const currentIds = new Set(current.filter((row) => row.id > 0).map((row) => row.id));
	const changes: IChange<T>[] = [];

	for (const row of current) {
		if (row.id <= 0) {
			changes.push({ changeType: EChangeType.Add, item: toItem(row) });
		} else {
			const previous = baseById.get(row.id);
			if (previous && !equals(row, previous)) {
				changes.push({ changeType: EChangeType.Edit, item: toItem(row) });
			}
		}
	}
	for (const row of base) {
		if (!currentIds.has(row.id)) {
			changes.push({ changeType: EChangeType.Delete, item: toItem(row) });
		}
	}
	return changes;
}

/**
 * Diffs a skill's effects into Add/Edit/Delete changes. Effects carry their own
 * id (≤ 0 marks a new, unsaved effect).
 */
export function skillEffectChanges(
	current: ISkillEffect[],
	baseline: ISkillEffect[] | undefined
): IChange<ISkillEffect>[] {
	return surrogateIdChanges(
		current,
		baseline,
		(a, b) =>
			a.target === b.target &&
			a.attributeId === b.attributeId &&
			a.modifierTypeId === b.modifierTypeId &&
			a.amount === b.amount &&
			a.durationMs === b.durationMs &&
			a.scalingAttributeId === b.scalingAttributeId &&
			a.scalingAmount === b.scalingAmount,
		// New, unsaved effects carry a unique negative client id; normalise it to 0 so the wire
		// payload marks an Add the same way regardless of the local id.
		(effect) => ({ ...effect, id: effect.id <= 0 ? 0 : effect.id })
	);
}

/**
 * Diffs an item's mod slots into Add/Edit/Delete changes. Slots carry their own
 * id (≤ 0 marks a new, unsaved slot); duplicates of the same type are allowed.
 */
export function modSlotChanges(
	current: IItemModSlot[],
	baseline: IItemModSlot[] | undefined,
	itemId: number
): IChange<IItemModSlot>[] {
	return surrogateIdChanges(
		current,
		baseline,
		(a, b) => a.itemModSlotTypeId === b.itemModSlotTypeId,
		(slot) => ({ id: slot.id <= 0 ? 0 : slot.id, itemId, itemModSlotTypeId: slot.itemModSlotTypeId })
	);
}
