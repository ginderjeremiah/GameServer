import { EChangeType, type IChange, type IItemModSlot, type ISkillEffect } from '$lib/api';
import type { Identified, SaveDiff } from './entities/types';

export const listsEqual = (a: unknown, b: unknown): boolean => JSON.stringify(a) === JSON.stringify(b);

/** True when a child collection differs from its saved baseline (added records have none). */
export const childChanged = <T>(current: T, baseline: T | undefined): boolean =>
	baseline === undefined || !listsEqual(current, baseline);

type ChildSaver<T> = (id: number, record: T, baseline: T | undefined) => Promise<void>;

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
 * Generic per-entity save: persists identity-level Add/Edit/Delete in one call,
 * refetches to resolve the ids of newly-added records (positionally — the backend
 * appends adds in send order), then runs each child-section persister for every
 * added/modified record against its real id. Returns the final saved list.
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

	if (changes.length) {
		await postPrimary(changes);
	}

	const fresh = await refresh();

	// Records present after save but absent before are the persisted adds; the
	// backend inserts them in send order, so the k-th added record maps to the
	// k-th lowest new id.
	const existing = new Set(diff.existingIds);
	const newlyPersisted = fresh.filter((record) => !existing.has(record.id)).sort((a, b) => a.id - b.id);
	const idFor = new Map<number, number>();
	diff.added.forEach((record, index) => {
		const persisted = newlyPersisted[index];
		if (persisted) {
			idFor.set(record.id, persisted.id);
		}
	});

	const childTargets: { id: number; record: T; baseline: T | undefined }[] = [
		...diff.added.map((record) => ({ id: idFor.get(record.id) ?? record.id, record, baseline: undefined })),
		...diff.modified.map(({ record, baseline }) => ({ id: record.id, record, baseline }))
	];

	if (childSavers.length && childTargets.length) {
		for (const target of childTargets) {
			for (const saver of childSavers) {
				await saver(target.id, target.record, target.baseline);
			}
		}
		return refresh();
	}

	return fresh;
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
 * Diffs a skill's effects into Add/Edit/Delete changes. Effects carry their own
 * id (≤ 0 marks a new, unsaved effect).
 */
export function skillEffectChanges(
	current: ISkillEffect[],
	baseline: ISkillEffect[] | undefined
): IChange<ISkillEffect>[] {
	const base = baseline ?? [];
	const baseById = new Map(base.filter((e) => e.id > 0).map((e) => [e.id, e]));
	const currentIds = new Set(current.filter((e) => e.id > 0).map((e) => e.id));
	const changes: IChange<ISkillEffect>[] = [];

	for (const effect of current) {
		if (effect.id <= 0) {
			changes.push({ changeType: EChangeType.Add, item: effect });
		} else {
			const previous = baseById.get(effect.id);
			if (
				previous &&
				(previous.target !== effect.target ||
					previous.attributeId !== effect.attributeId ||
					previous.modifierTypeId !== effect.modifierTypeId ||
					previous.amount !== effect.amount ||
					previous.durationMs !== effect.durationMs)
			) {
				changes.push({ changeType: EChangeType.Edit, item: effect });
			}
		}
	}
	for (const effect of base) {
		if (!currentIds.has(effect.id)) {
			changes.push({ changeType: EChangeType.Delete, item: effect });
		}
	}
	return changes;
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
	const base = baseline ?? [];
	const baseById = new Map(base.filter((slot) => slot.id > 0).map((slot) => [slot.id, slot]));
	const currentIds = new Set(current.filter((slot) => slot.id > 0).map((slot) => slot.id));
	const changes: IChange<IItemModSlot>[] = [];

	for (const slot of current) {
		if (slot.id <= 0) {
			changes.push({ changeType: EChangeType.Add, item: { id: 0, itemId, itemModSlotTypeId: slot.itemModSlotTypeId } });
		} else {
			const previous = baseById.get(slot.id);
			if (previous && previous.itemModSlotTypeId !== slot.itemModSlotTypeId) {
				changes.push({
					changeType: EChangeType.Edit,
					item: { id: slot.id, itemId, itemModSlotTypeId: slot.itemModSlotTypeId }
				});
			}
		}
	}
	for (const slot of base) {
		if (!currentIds.has(slot.id)) {
			changes.push({
				changeType: EChangeType.Delete,
				item: { id: slot.id, itemId, itemModSlotTypeId: slot.itemModSlotTypeId }
			});
		}
	}
	return changes;
}
