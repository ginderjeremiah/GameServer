import { SvelteMap, SvelteSet } from 'svelte/reactivity';
import { SaveFlash } from '$lib/common';
import { toastError } from '$stores';
import { canonicalEqual, PersistFailedError, type PersistRecovery } from './save-helpers';
import { entityBlockingWarnings, entityWarnings } from './validation';
import type { EntityConfig, Identified, SaveDiff } from './entities/types';

export type RecordStatus = 'clean' | 'added' | 'modified' | 'deleted';

/** A record's change status plus its validation warnings — memoised together off the store. */
export interface RecordState {
	status: RecordStatus;
	warnings: string[];
	/** The subset of {@link warnings} the backend is known to hard-reject a save over. */
	blockingWarnings: string[];
}

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));
export const recordsEqual = canonicalEqual;

/**
 * Working store for one entity: holds an editable copy of the saved records plus
 * a baseline, and derives per-record status (added / modified / deleted) by
 * comparing against that baseline. Save translates the diff into API calls via
 * the entity config and commits a new baseline; discard reverts to it.
 */
export class EntityStore<T extends Identified> {
	private config: EntityConfig<T>;

	items = $state<T[]>([]);
	private base = $state<T[]>([]);
	private deleted = new SvelteSet<number>();
	#saveFlash = new SaveFlash();
	saving = $state(false);
	/** Maps a just-saved record's local (negative) id to its persisted id, so a caller tracking a
	 *  selection by id (the Workbench detail pane) can follow a newly-added record across a save
	 *  instead of losing it to the "record no longer found" fallback. Replaced wholesale each save. */
	lastIdMap = $state<SvelteMap<number, number>>(new SvelteMap());
	private nextId = -1;

	constructor(config: EntityConfig<T>, seed: T[]) {
		this.config = config;
		this.items = seed.map(clone);
		this.base = seed.map(clone);
	}

	/** Brief "Changes saved" confirmation flash; writable so a mutator can clear it directly
	 *  (see {@link patch}/{@link removeItem}/etc.) without going through {@link save}. */
	get saved(): boolean {
		return this.#saveFlash.active;
	}

	set saved(value: boolean) {
		this.#saveFlash.active = value;
	}

	private baseMap = $derived.by(() => {
		const map: Record<number, T> = {};
		for (const record of this.base) {
			map[record.id] = record;
		}
		return map;
	});

	baselineOf(id: number): T | undefined {
		return this.baseMap[id];
	}

	status(record: T): RecordStatus {
		if (this.deleted.has(record.id)) {
			return 'deleted';
		}
		const baseline = this.baseMap[record.id];
		if (!baseline) {
			return 'added';
		}
		return recordsEqual(record, baseline) ? 'clean' : 'modified';
	}

	/** Bumped whenever a record's status can change *without* its object reference changing — only
	 *  {@link removeItem}/{@link restoreItem} toggling a saved record's membership in {@link deleted}.
	 *  Every other mutator ({@link patch}, {@link addItem}, {@link resetItem}, `save`, `discard`)
	 *  replaces the affected record(s) with a new object, which already misses the reference cache
	 *  below on its own. */
	private statusEpoch = $state(0);
	private stateCacheEpoch = -1;
	private stateCache = new WeakMap<T, RecordState>();

	/**
	 * Per-record status + validation warnings, keyed by id. Memoised **per record object** so an edit
	 * keystroke — which replaces only the one record {@link patch} touched — re-diffs just that record
	 * instead of re-canonicalizing the whole catalogue; every other record is a same-reference cache
	 * hit. The cache is a `WeakMap` keyed on the record itself (not its id) so a superseded record from
	 * an earlier edit is garbage-collected instead of accumulating for the life of the store.
	 */
	recordStates = $derived.by<Record<number, RecordState>>(() => {
		if (this.statusEpoch !== this.stateCacheEpoch) {
			this.stateCache = new WeakMap();
			this.stateCacheEpoch = this.statusEpoch;
		}
		const map: Record<number, RecordState> = {};
		for (const record of this.items) {
			let state = this.stateCache.get(record);
			if (!state) {
				state = {
					status: this.status(record),
					warnings: entityWarnings(this.config, record),
					blockingWarnings: entityBlockingWarnings(this.config, record)
				};
				this.stateCache.set(record, state);
			}
			map[record.id] = state;
		}
		return map;
	});

	/** The memoised status + warnings for a record (recomputed on the fly for an unknown id). */
	stateOf(record: T): RecordState {
		return (
			this.recordStates[record.id] ?? {
				status: this.status(record),
				warnings: entityWarnings(this.config, record),
				blockingWarnings: entityBlockingWarnings(this.config, record)
			}
		);
	}

	/** True while any not-yet-saved record (added/modified — a delete has nothing left to validate)
	 *  carries a warning the backend is known to hard-reject a save over. Gates {@link save} so a
	 *  predictable failure can't trigger the partial-failure rebase path in the first place (#2217). */
	hasBlockingWarnings = $derived.by(() => {
		return Object.values(this.recordStates).some(
			(state) => (state.status === 'added' || state.status === 'modified') && state.blockingWarnings.length > 0
		);
	});

	counts = $derived.by(() => {
		let added = 0;
		let modified = 0;
		let deleted = 0;
		for (const { status } of Object.values(this.recordStates)) {
			switch (status) {
				case 'added':
					added++;
					break;
				case 'modified':
					modified++;
					break;
				case 'deleted':
					deleted++;
					break;
			}
		}
		return { added, modified, deleted, total: added + modified + deleted };
	});

	/**
	 * Edits made while a save is in flight would either be silently overwritten by that save's
	 * post-persist baseline replacement or would land as a "clean" record whose dirty indicator
	 * never fires — so every mutator no-ops while {@link saving} is true rather than risk either.
	 */
	patch(id: number, mutate: (draft: T) => void) {
		if (this.saving) {
			return;
		}
		this.items = this.items.map((record) => {
			if (record.id !== id) {
				return record;
			}
			const draft = clone(record);
			mutate(draft);
			return draft;
		});
		this.saved = false;
	}

	addItem(): number {
		if (this.saving) {
			return this.items[0]?.id ?? 0;
		}
		const id = this.nextId--;
		this.items = [this.config.newItem(id), ...this.items];
		this.saved = false;
		return id;
	}

	removeItem(id: number) {
		if (this.saving) {
			return;
		}
		if (this.baseMap[id] === undefined) {
			// Never-saved record: just drop it.
			this.items = this.items.filter((record) => record.id !== id);
		} else {
			// The record's own object reference is untouched, so the status cache must be invalidated
			// explicitly — nothing else would tell recordStates this record just became 'deleted'.
			this.deleted.add(id);
			this.statusEpoch++;
		}
		this.saved = false;
	}

	restoreItem(id: number) {
		if (this.saving) {
			return;
		}
		// Same reasoning as removeItem: the reference is unchanged, so bump the epoch to invalidate.
		this.deleted.delete(id);
		this.statusEpoch++;
		this.saved = false;
	}

	/** True when a record is retired (out of circulation but kept at its slot and resolvable by id). */
	isRetired(record: T): boolean {
		return record.retiredAt != null;
	}

	/**
	 * Retire or reinstate a saved reference record. Retiring stamps a timestamp; reinstating clears it
	 * back to null (matching the server's serialized "active" shape). This is an ordinary edit — the
	 * record reads as `modified` and persists through the normal Add/Edit path — never a hard delete.
	 */
	setRetired(id: number, retired: boolean) {
		// Throwaway Date used only to format a one-shot ISO timestamp — not reactive state.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const retiredAt = retired ? new Date().toISOString() : null;
		this.patch(id, (draft) => {
			draft.retiredAt = retiredAt;
		});
	}

	resetItem(id: number) {
		if (this.saving) {
			return;
		}
		const baseline = this.baseMap[id];
		if (baseline) {
			this.items = this.items.map((record) => (record.id === id ? clone(baseline) : record));
		}
		this.deleted.delete(id);
		this.saved = false;
	}

	private diff(): SaveDiff<T> {
		const added: T[] = [];
		const modified: { record: T; baseline: T }[] = [];
		const deleted: T[] = [];
		for (const record of this.items) {
			const status = this.status(record);
			if (status === 'added') {
				added.push(clone(record));
			} else if (status === 'modified') {
				modified.push({ record: clone(record), baseline: clone(this.baseMap[record.id]) });
			}
		}
		for (const id of this.deleted) {
			const baseline = this.baseMap[id];
			if (baseline) {
				deleted.push(clone(baseline));
			}
		}
		return { added, modified, deleted, existingIds: this.base.map((record) => record.id) };
	}

	async save() {
		if (this.counts.total === 0 || this.saving || this.hasBlockingWarnings) {
			return;
		}
		this.saving = true;
		try {
			const diff = this.diff();
			const { records, idMap } = await this.config.persist(diff);
			this.lastIdMap = new SvelteMap(idMap);
			this.items = records.map(clone);
			this.base = records.map(clone);
			this.deleted.clear();
			this.#saveFlash.flash();
		} catch (ex) {
			// A *partial* failure (the identity Add/Edit batch or an earlier child saver committed,
			// then a later step threw) leaves our baseline behind the server for the records this
			// save's diff actually touched, so those must be rebased against server truth — a naive
			// retry could re-Add an already-persisted record. Every *other* pending edit (on a record
			// this save never touched, or one whose writes hadn't reached yet) is kept for a clean
			// retry rather than discarded (#2207). A *pre-commit* failure committed nothing, so all
			// edits are left in place. `recovery` is only absent when the post-commit refetch itself
			// failed, in which case there's nothing to rebase against and we fall back to a full re-seed.
			if (ex instanceof PersistFailedError) {
				const recovery = ex.recovery as PersistRecovery<T> | undefined;
				if (recovery && this.diffAddsResolved(recovery.idMap)) {
					this.rebaseAfterPartialFailure(recovery);
				} else if (recovery) {
					// A committed add whose persisted id the refetch never resolved (e.g. a concurrent
					// delete by another admin skewed the count, #1856) can't be safely rebased — keeping
					// it pending under its local id would re-Add a duplicate on retry. Fall back to a full
					// re-seed from the recovery's own refetch (already fresh; no need to hit the network
					// again), the same as the pre-existing safe behavior for an unrebaseable failure.
					this.items = recovery.fresh.map(clone);
					this.base = recovery.fresh.map(clone);
					this.deleted.clear();
				} else {
					try {
						const fresh = await this.config.refresh();
						this.items = fresh.map(clone);
						this.base = fresh.map(clone);
						this.deleted.clear();
					} catch {
						// Leave local state intact; surfacing the original save error is what matters.
					}
				}
			}
			toastError(ex instanceof Error ? ex.message : 'Failed to save changes.');
		} finally {
			this.saving = false;
		}
	}

	/**
	 * A rebase remaps every currently-`added` record through `idMap` (its only route from a local
	 * negative id to a persisted one). If the refetch never resolved one — the add's identity batch
	 * still committed, so it exists server-side — a rebase would keep it pending under its local id,
	 * and a retry would re-Add it as a duplicate. Every record with `added` status was necessarily
	 * part of this save's own diff (mutators no-op while {@link saving} is true), so this check is
	 * exactly the records a rebase would need to remap.
	 */
	private diffAddsResolved(idMap: Map<number, number>): boolean {
		return this.items.every((record) => this.status(record) !== 'added' || idMap.has(record.id));
	}

	/**
	 * Rebases this save's own diff against server truth instead of blanket-reseeding the whole
	 * catalogue: a record this save fully settled (identity + every child saver) takes the fresh
	 * server copy, but anything else — an untouched sibling, or a record this save's failure left
	 * only partially written — keeps its current local state so the admin can retry without redoing
	 * unrelated work. A newly-added record that settled is remapped from its local (negative) id
	 * onto the persisted id assigned during this save. A delete has no child saver, so it always
	 * settles atomically with the primary batch — {@link PersistRecovery.settledIds} already
	 * guarantees this, so no delete can ever survive a rebase as still-pending. Only called once
	 * {@link diffAddsResolved} has confirmed every added record maps to a persisted id.
	 */
	private rebaseAfterPartialFailure({ fresh, idMap, settledIds }: PersistRecovery<T>) {
		// Throwaway locals scoped to this one synchronous pass, not reactive state.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const freshById = new Map(fresh.map((record) => [record.id, record]));
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const claimedIds = new Set<number>();
		const rebased: T[] = [];

		for (const record of this.items) {
			const status = this.status(record);
			const persistedId = record.id < 0 ? idMap.get(record.id) : record.id;
			const settled = status === 'clean' || (persistedId !== undefined && settledIds.has(persistedId));

			if (settled) {
				// Server truth for a settled record — absent from `fresh` only when this save's own
				// delete for it committed, in which case it's simply dropped.
				const freshRecord = persistedId !== undefined ? freshById.get(persistedId) : undefined;
				if (freshRecord) {
					rebased.push(clone(freshRecord));
					claimedIds.add(freshRecord.id);
				}
				continue;
			}

			// Added/modified but not yet fully written: keep the local edit, remapped onto its
			// persisted id if this save's primary batch already assigned it one.
			const rebasedRecord =
				persistedId !== undefined && persistedId !== record.id ? { ...clone(record), id: persistedId } : clone(record);
			rebased.push(rebasedRecord);
			if (persistedId !== undefined) {
				claimedIds.add(persistedId);
			}
		}

		// A record this save's diff never touched at all but that showed up in the refetch anyway
		// (a concurrent add from another admin) still needs to appear.
		for (const record of fresh) {
			if (!claimedIds.has(record.id)) {
				rebased.push(clone(record));
			}
		}

		this.items = rebased;
		this.base = fresh.map(clone);
		this.deleted.clear();
	}

	discard() {
		this.items = this.base.map(clone);
		this.deleted.clear();
		this.saved = false;
	}

	dispose() {
		this.#saveFlash.dispose();
	}
}
