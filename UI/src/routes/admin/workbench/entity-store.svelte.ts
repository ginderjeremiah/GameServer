import { SvelteMap, SvelteSet } from 'svelte/reactivity';
import { SaveFlash } from '$lib/common';
import { toastError } from '$stores';
import { canonicalEqual, PersistFailedError } from './save-helpers';
import { entityWarnings } from './validation';
import type { EntityConfig, Identified, SaveDiff } from './entities/types';

export type RecordStatus = 'clean' | 'added' | 'modified' | 'deleted';

/** A record's change status plus its validation warnings — memoised together off the store. */
export interface RecordState {
	status: RecordStatus;
	warnings: string[];
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
				state = { status: this.status(record), warnings: entityWarnings(this.config, record) };
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
				warnings: entityWarnings(this.config, record)
			}
		);
	}

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
		if (this.counts.total === 0 || this.saving) {
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
			// then a later step threw) leaves our baseline behind the server: the persisted adds are
			// still local "added" records, so a naive retry would re-Add duplicates. Only then do we
			// re-seed from server truth. A *pre-commit* failure committed nothing, so we leave the
			// edits in place for a clean retry rather than blowing them away behind a toast.
			if (ex instanceof PersistFailedError) {
				try {
					const fresh = await this.config.refresh();
					this.items = fresh.map(clone);
					this.base = fresh.map(clone);
					this.deleted.clear();
				} catch {
					// Leave local state intact; surfacing the original save error is what matters.
				}
			}
			toastError(ex instanceof Error ? ex.message : 'Failed to save changes.');
		} finally {
			this.saving = false;
		}
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
