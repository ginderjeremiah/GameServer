import { SvelteSet } from 'svelte/reactivity';
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
	saved = $state(false);
	saving = $state(false);
	private nextId = -1;
	#flashTimer: ReturnType<typeof setTimeout> | undefined;

	constructor(config: EntityConfig<T>, seed: T[]) {
		this.config = config;
		this.items = seed.map(clone);
		this.base = seed.map(clone);
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

	/**
	 * Per-record status + validation warnings, keyed by id. Memoised here so the consumers (the list
	 * rows, the page summary, the save-bar counts) read O(1) lookups instead of each re-running the
	 * structural diff per record. Recomputes only when the records, baseline, or pending deletes
	 * change — a search keystroke or a selection change (which touch neither) reuses the cached map.
	 */
	recordStates = $derived.by<Record<number, RecordState>>(() => {
		const map: Record<number, RecordState> = {};
		for (const record of this.items) {
			map[record.id] = { status: this.status(record), warnings: entityWarnings(this.config, record) };
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

	patch(id: number, mutate: (draft: T) => void) {
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
		const id = this.nextId--;
		this.items = [this.config.newItem(id), ...this.items];
		this.saved = false;
		return id;
	}

	removeItem(id: number) {
		if (this.baseMap[id] === undefined) {
			// Never-saved record: just drop it.
			this.items = this.items.filter((record) => record.id !== id);
		} else {
			this.deleted.add(id);
		}
		this.saved = false;
	}

	restoreItem(id: number) {
		this.deleted.delete(id);
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
			const fresh = await this.config.persist(this.diff());
			this.items = fresh.map(clone);
			this.base = fresh.map(clone);
			this.deleted.clear();
			this.flashSaved();
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

	/** Briefly flash the "Changes saved" confirmation. The timer handle is owned so a
	 *  re-arm clears the prior one and {@link dispose} can cancel a pending flash — a save
	 *  that lands just before the Workbench unmounts must not write into a dead store. */
	private flashSaved() {
		this.saved = true;
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
		this.#flashTimer = setTimeout(() => {
			this.saved = false;
		}, 1900);
	}

	dispose() {
		if (this.#flashTimer) {
			clearTimeout(this.#flashTimer);
		}
	}
}
