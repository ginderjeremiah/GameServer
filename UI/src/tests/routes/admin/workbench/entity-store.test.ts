import { describe, it, expect, vi } from 'vitest';

vi.mock('$stores', () => ({ toastError: vi.fn() }));

import { EntityStore } from '../../../../routes/admin/workbench/entity-store.svelte';
import { PersistFailedError } from '../../../../routes/admin/workbench/save-helpers';
import type { EntityConfig, Identified, SaveDiff } from '../../../../routes/admin/workbench/entities/types';

interface Row extends Identified {
	id: number;
	name: string;
	value: number;
}

/** Wraps a mock's records into the {@link EntityConfig.persist} return shape (an empty idMap unless supplied). */
const persistResult = (records: Row[], idMap = new Map<number, number>()) => ({ records, idMap });

const makeConfig = (
	persist: (diff: SaveDiff<Row>) => Promise<{ records: Row[]; idMap: Map<number, number> }> = async () =>
		persistResult([])
): EntityConfig<Row> => ({
	key: 'rows',
	label: 'Rows',
	singular: 'Row',
	glyph: 'box',
	blankName: 'Unnamed',
	newItem: (id) => ({ id, name: '', value: 0 }),
	meta: () => [],
	sections: [],
	refresh: async () => [],
	persist
});

const seed: Row[] = [
	{ id: 0, name: 'Alpha', value: 1 },
	{ id: 1, name: 'Beta', value: 2 }
];

describe('EntityStore', () => {
	it('starts clean with the seeded records', () => {
		const store = new EntityStore(makeConfig(), seed);
		expect(store.items).toHaveLength(2);
		expect(store.counts.total).toBe(0);
		expect(store.status(store.items[0])).toBe('clean');
	});

	it('does not mutate the seed array', () => {
		const store = new EntityStore(makeConfig(), seed);
		store.patch(0, (draft) => (draft.value = 99));
		expect(seed[0].value).toBe(1);
	});

	it('flags an added record and counts it', () => {
		const store = new EntityStore(makeConfig(), seed);
		const id = store.addItem();
		expect(id).toBeLessThan(0);
		expect(store.items[0].id).toBe(id);
		expect(store.status(store.items[0])).toBe('added');
		expect(store.counts.added).toBe(1);
		expect(store.counts.total).toBe(1);
	});

	it('marks a record modified and returns to clean when reverted', () => {
		const store = new EntityStore(makeConfig(), seed);
		store.patch(1, (draft) => (draft.value = 50));
		expect(store.status(store.items.find((r) => r.id === 1)!)).toBe('modified');
		expect(store.counts.modified).toBe(1);

		store.patch(1, (draft) => (draft.value = 2));
		expect(store.status(store.items.find((r) => r.id === 1)!)).toBe('clean');
		expect(store.counts.total).toBe(0);
	});

	it('drops never-saved records on remove but marks saved ones deleted', () => {
		const store = new EntityStore(makeConfig(), seed);
		const newId = store.addItem();
		store.removeItem(newId);
		expect(store.items.find((r) => r.id === newId)).toBeUndefined();

		store.removeItem(0);
		expect(store.status(store.items.find((r) => r.id === 0)!)).toBe('deleted');
		expect(store.counts.deleted).toBe(1);
	});

	it('restores a deleted record and resets a modified one', () => {
		const store = new EntityStore(makeConfig(), seed);
		store.removeItem(0);
		store.restoreItem(0);
		expect(store.status(store.items.find((r) => r.id === 0)!)).toBe('clean');

		store.patch(1, (draft) => (draft.value = 7));
		store.resetItem(1);
		expect(store.items.find((r) => r.id === 1)!.value).toBe(2);
		expect(store.counts.total).toBe(0);
	});

	it('clears the saved flag on restore so the "Changes saved" banner cannot linger', () => {
		const store = new EntityStore(makeConfig(), seed);
		store.saved = true;
		store.restoreItem(0);
		expect(store.saved).toBe(false);
	});

	it('discard reverts every pending change', () => {
		const store = new EntityStore(makeConfig(), seed);
		store.addItem();
		store.patch(0, (draft) => (draft.name = 'changed'));
		store.removeItem(1);
		expect(store.counts.total).toBe(3);

		store.discard();
		expect(store.counts.total).toBe(0);
		expect(store.items).toHaveLength(2);
		expect(store.items.find((r) => r.id === 0)!.name).toBe('Alpha');
	});

	it('save passes the diff to persist and adopts the returned baseline', async () => {
		const fresh: Row[] = [
			{ id: 0, name: 'Alpha', value: 1 },
			{ id: 1, name: 'Beta edited', value: 2 },
			{ id: 2, name: 'Gamma', value: 3 }
		];
		let captured: SaveDiff<Row> | undefined;
		const persist = vi.fn(async (diff: SaveDiff<Row>) => {
			captured = diff;
			return persistResult(fresh);
		});
		const store = new EntityStore(makeConfig(persist), seed);

		store.addItem();
		store.patch(1, (draft) => (draft.name = 'Beta edited'));
		await store.save();

		expect(persist).toHaveBeenCalledOnce();
		expect(captured?.added).toHaveLength(1);
		expect(captured?.modified).toHaveLength(1);
		expect(captured?.existingIds).toEqual([0, 1]);

		expect(store.counts.total).toBe(0);
		expect(store.items).toHaveLength(3);
		expect(store.saved).toBe(true);
	});

	it("maps a saved record's temporary negative id to its persisted id", async () => {
		const fresh: Row[] = [
			{ id: 0, name: 'Alpha', value: 1 },
			{ id: 1, name: 'Beta', value: 2 },
			{ id: 2, name: 'Gamma', value: 3 }
		];
		const store = new EntityStore(
			// A real config's persist (via persistEntity) resolves the idMap from its own diff.added;
			// this mirrors that instead of hardcoding it, so the test still exercises the mapping.
			makeConfig(async (diff) => persistResult(fresh, new Map(diff.added.map((record) => [record.id, 2])))),
			seed
		);
		const newId = store.addItem();

		await store.save();

		expect(store.lastIdMap.get(newId)).toBe(2);
	});

	it('replaces the id map wholesale on a later save with no added records', async () => {
		const fresh: Row[] = [...seed, { id: 2, name: 'Gamma', value: 3 }];
		const persist = vi.fn(async (diff: SaveDiff<Row>) =>
			persistResult(fresh, new Map(diff.added.map((record, index) => [record.id, 2 + index])))
		);
		const store = new EntityStore(makeConfig(persist), seed);

		store.addItem();
		await store.save();
		expect(store.lastIdMap.size).toBe(1);

		store.patch(0, (draft) => (draft.value = 42));
		await store.save();
		expect(store.lastIdMap.size).toBe(0);
	});

	it('re-seeds from server truth on a partial (committed) failure, so a retry cannot re-add duplicates', async () => {
		// A partial failure: the add committed server-side (now id 2) but a child saver then threw,
		// surfacing as PersistFailedError.
		const serverTruth: Row[] = [
			{ id: 0, name: 'Alpha', value: 1 },
			{ id: 1, name: 'Beta', value: 2 },
			{ id: 2, name: 'Gamma', value: 3 }
		];
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new PersistFailedError(new Error('child saver failed'));
			}),
			refresh: async () => serverTruth
		};
		const store = new EntityStore(config, seed);
		store.addItem();
		expect(store.counts.added).toBe(1);

		await store.save();

		// The negative-id "added" record is gone — the screen reflects what actually persisted, so
		// a retry diffs against server truth instead of re-Adding the already-persisted record.
		expect(store.counts.total).toBe(0);
		expect(store.items).toHaveLength(3);
		expect(store.items.some((r) => r.id < 0)).toBe(false);
	});

	it('keeps local edits on a total (pre-commit) failure so a retry does not lose work', async () => {
		// A plain Error (not PersistFailedError) means nothing committed — the user's edits must
		// survive, and we must not re-seed from server truth behind a toast.
		const serverTruth: Row[] = [...seed];
		const refresh = vi.fn(async () => serverTruth);
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new Error('primary batch 500');
			}),
			refresh
		};
		const store = new EntityStore(config, seed);
		store.addItem();
		store.patch(0, (draft) => (draft.name = 'edited'));

		await store.save();

		expect(store.counts.added).toBe(1);
		expect(store.items.find((r) => r.id === 0)?.name).toBe('edited');
		// No recovery refresh is issued on a pre-commit failure.
		expect(refresh).not.toHaveBeenCalled();
	});

	it('keeps local edits when the partial-failure recovery refresh also fails', async () => {
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new PersistFailedError(new Error('child saver failed'));
			}),
			refresh: async () => {
				throw new Error('refresh failed');
			}
		};
		const store = new EntityStore(config, seed);
		store.addItem();

		await store.save();

		expect(store.counts.added).toBe(1);
	});

	it("rebases only the unsettled slice of a partial failure, keeping a sibling record's pending edit alive for a clean retry (#2207)", async () => {
		const baseline: Row[] = [
			{ id: 0, name: 'Alpha', value: 1 },
			{ id: 1, name: 'Beta', value: 2 },
			{ id: 2, name: 'Gamma', value: 3 }
		];
		// The identity (name) batch commits for everyone; only record 0's child-managed `value` write
		// actually lands before record 1's throws and aborts the rest of the batch.
		const fresh: Row[] = [
			{ id: 0, name: 'Alpha-edited', value: 101 },
			{ id: 1, name: 'Beta-edited', value: 2 },
			{ id: 2, name: 'Gamma-edited', value: 3 }
		];
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new PersistFailedError(new Error('child saver failed'), {
					fresh,
					idMap: new Map(),
					settledIds: new Set([0])
				});
			}),
			refresh: async () => fresh
		};
		const store = new EntityStore(config, baseline);
		store.patch(0, (draft) => {
			draft.name = 'Alpha-edited';
			draft.value = 101;
		});
		store.patch(1, (draft) => {
			draft.name = 'Beta-edited';
			draft.value = 102;
		});
		store.patch(2, (draft) => {
			draft.name = 'Gamma-edited';
			draft.value = 103;
		});

		await store.save();

		// Record 0 fully settled — server truth wins (it matches what was actually sent anyway).
		expect(store.items.find((r) => r.id === 0)).toEqual({ id: 0, name: 'Alpha-edited', value: 101 });
		// Records 1 and 2 never finished their child write — the pending edit survives instead of
		// being silently discarded behind the toast, and each still reads as modified for a retry
		// that now only needs to resend the child-managed part (identity already matches baseline).
		expect(store.items.find((r) => r.id === 1)).toEqual({ id: 1, name: 'Beta-edited', value: 102 });
		expect(store.status(store.items.find((r) => r.id === 1)!)).toBe('modified');
		expect(store.items.find((r) => r.id === 2)).toEqual({ id: 2, name: 'Gamma-edited', value: 103 });
		expect(store.status(store.items.find((r) => r.id === 2)!)).toBe('modified');
		expect(store.counts.total).toBe(2);
	});

	it('remaps a settled newly-added record from its local id to its persisted id', async () => {
		const fresh: Row[] = [...seed, { id: 2, name: 'Gamma', value: 3 }];
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new PersistFailedError(new Error('an unrelated child saver failed'), {
					fresh,
					idMap: new Map([[-1, 2]]),
					settledIds: new Set([2])
				});
			}),
			refresh: async () => fresh
		};
		const store = new EntityStore(config, seed);
		store.addItem();

		await store.save();

		expect(store.items.some((r) => r.id < 0)).toBe(false);
		expect(store.items.find((r) => r.id === 2)).toEqual({ id: 2, name: 'Gamma', value: 3 });
		expect(store.counts.total).toBe(0);
	});

	it('keeps an unsettled newly-added record pending (remapped to its persisted id) instead of discarding its edit', async () => {
		// The add's identity committed (so its id resolves to 2), but its own child saver hasn't
		// finished — the record is not yet in settledIds.
		const fresh: Row[] = [...seed, { id: 2, name: '', value: 0 }];
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new PersistFailedError(new Error('this record’s own child saver failed'), {
					fresh,
					idMap: new Map([[-1, 2]]),
					settledIds: new Set()
				});
			}),
			refresh: async () => fresh
		};
		const store = new EntityStore(config, seed);
		const newId = store.addItem();
		store.patch(newId, (draft) => (draft.value = 999));

		await store.save();

		const record = store.items.find((r) => r.id === 2);
		expect(record).toEqual({ id: 2, name: '', value: 999 });
		expect(store.status(record!)).toBe('modified');
		expect(store.counts.total).toBe(1);
	});

	it('falls back to a full re-seed (not a rebase) when a committed add could not be resolved to a persisted id, so a retry cannot re-Add a duplicate', async () => {
		// Mirrors persistEntity's `resolveId`-throws path (save-helpers.ts): the add's identity batch
		// committed server-side, but the post-commit refetch never mapped it (e.g. a concurrent delete
		// by another admin skewed the count, #1856) — `idMap` has no entry for it. Rebasing would keep
		// the add pending under its local id and re-Add a duplicate on the next save.
		const fresh: Row[] = [...seed, { id: 2, name: 'A', value: 0 }];
		const config: EntityConfig<Row> = {
			...makeConfig(async () => {
				throw new PersistFailedError(new Error('no persisted id found for record'), {
					fresh,
					idMap: new Map(),
					settledIds: new Set()
				});
			}),
			refresh: async () => fresh
		};
		const store = new EntityStore(config, seed);
		store.addItem();

		await store.save();

		// The orphaned local add is dropped, not kept pending, so a retry can't duplicate it.
		expect(store.items.some((r) => r.id < 0)).toBe(false);
		expect(store.counts.added).toBe(0);
		expect(store.items).toHaveLength(3);
	});

	it('does not call persist when there are no changes', async () => {
		const persist = vi.fn(async () => persistResult(seed));
		const store = new EntityStore(makeConfig(persist), seed);
		await store.save();
		expect(persist).not.toHaveBeenCalled();
	});

	describe('saved flash timer', () => {
		it('clears the saved flag after the flash interval elapses', async () => {
			vi.useFakeTimers();
			try {
				const store = new EntityStore(
					makeConfig(async () => persistResult(seed)),
					seed
				);
				store.addItem();
				await store.save();
				expect(store.saved).toBe(true);

				vi.advanceTimersByTime(1900);
				expect(store.saved).toBe(false);
			} finally {
				vi.useRealTimers();
			}
		});

		it('dispose cancels a pending flash so saved is not flipped after teardown', async () => {
			vi.useFakeTimers();
			try {
				const store = new EntityStore(
					makeConfig(async () => persistResult(seed)),
					seed
				);
				store.addItem();
				await store.save();
				expect(store.saved).toBe(true);

				// Teardown (Workbench unmount) before the flash fires must not leave a
				// live timer that writes into the dead store.
				store.dispose();
				vi.advanceTimersByTime(1900);
				expect(store.saved).toBe(true);
				expect(vi.getTimerCount()).toBe(0);
			} finally {
				vi.useRealTimers();
			}
		});

		it('re-arming the flash clears the prior timer (no early reset)', async () => {
			vi.useFakeTimers();
			try {
				const store = new EntityStore(
					makeConfig(async () => persistResult(seed)),
					seed
				);
				store.addItem();
				await store.save();

				// A second save part-way through the first flash should restart, not stack.
				vi.advanceTimersByTime(1000);
				store.addItem();
				await store.save();
				expect(vi.getTimerCount()).toBe(1);

				// The first timer would have fired here; the re-armed one must keep saved true.
				vi.advanceTimersByTime(900);
				expect(store.saved).toBe(true);

				vi.advanceTimersByTime(1000);
				expect(store.saved).toBe(false);
			} finally {
				vi.useRealTimers();
			}
		});
	});

	describe('retire', () => {
		it('isRetired reflects the retiredAt field', () => {
			const store = new EntityStore(makeConfig(), [
				{ id: 0, name: 'Alpha', value: 1 },
				{ id: 1, name: 'Beta', value: 2, retiredAt: '2026-01-01T00:00:00Z' }
			]);
			expect(store.isRetired(store.items.find((r) => r.id === 0)!)).toBe(false);
			expect(store.isRetired(store.items.find((r) => r.id === 1)!)).toBe(true);
		});

		it('setRetired stamps retiredAt as an ordinary edit and keeps the record at its slot', () => {
			const store = new EntityStore(makeConfig(), seed);
			store.setRetired(0, true);

			const record = store.items.find((r) => r.id === 0)!;
			expect(store.isRetired(record)).toBe(true);
			expect(record.retiredAt).toBeTruthy();
			// A retire is a modify, never a delete — the slot is preserved so index lookups can't shift.
			expect(store.status(record)).toBe('modified');
			expect(store.counts.deleted).toBe(0);
			expect(store.items).toHaveLength(2);
		});

		it('reinstating clears retiredAt back to the active (null) shape', () => {
			const store = new EntityStore(makeConfig(), [
				{ id: 0, name: 'Alpha', value: 1, retiredAt: '2026-01-01T00:00:00Z' }
			]);
			store.setRetired(0, false);

			const record = store.items.find((r) => r.id === 0)!;
			expect(store.isRetired(record)).toBe(false);
			expect(record.retiredAt).toBeNull();
			expect(store.status(record)).toBe('modified');
		});
	});

	describe('mutators guard against an in-flight save', () => {
		it('patch no-ops while saving so a keystroke landing mid-save cannot be silently discarded', () => {
			const store = new EntityStore(makeConfig(), seed);
			store.saving = true;
			store.patch(0, (draft) => (draft.name = 'raced'));
			expect(store.items.find((r) => r.id === 0)!.name).toBe('Alpha');
		});

		it('addItem, removeItem, restoreItem and resetItem all no-op while saving', () => {
			const store = new EntityStore(makeConfig(), seed);
			store.saving = true;

			expect(store.addItem()).toBe(0);
			expect(store.items).toHaveLength(2);

			store.removeItem(0);
			expect(store.status(store.items.find((r) => r.id === 0)!)).toBe('clean');

			store.saving = false;
			store.patch(1, (draft) => (draft.value = 7));
			store.saving = true;

			store.resetItem(1);
			expect(store.items.find((r) => r.id === 1)!.value).toBe(7);

			store.removeItem(1);
			store.saving = false;
			store.removeItem(1);
			store.saving = true;
			store.restoreItem(1);
			expect(store.status(store.items.find((r) => r.id === 1)!)).toBe('deleted');
		});
	});

	describe('recordStates memo', () => {
		// A config with a required-name field so warnings are exercised alongside status.
		const requiredNameConfig = (): EntityConfig<Row> => ({
			...makeConfig(),
			sections: [
				{
					kind: 'fields',
					key: 'identity',
					label: 'Identity',
					glyph: 'box',
					fields: [{ key: 'name', label: 'Name', type: 'text', required: true }]
				}
			]
		});

		it('exposes per-record status + validation warnings keyed by id', () => {
			const store = new EntityStore(requiredNameConfig(), seed);
			store.patch(1, (draft) => (draft.value = 99));

			expect(store.recordStates[0].status).toBe('clean');
			expect(store.recordStates[1].status).toBe('modified');
			expect(store.recordStates[0].warnings).toEqual([]);
		});

		it('surfaces a validation warning for a record that violates a required field', () => {
			const store = new EntityStore(requiredNameConfig(), seed);
			const id = store.addItem(); // a fresh record has a blank (required) name

			const state = store.stateOf(store.items.find((r) => r.id === id)!);
			expect(state.status).toBe('added');
			expect(state.warnings).toEqual(['Name required']);
		});

		it('stateOf recomputes for an id absent from the memo (defensive fallback)', () => {
			const store = new EntityStore(requiredNameConfig(), seed);
			// A record not in the store still resolves to a sensible state rather than throwing.
			expect(store.stateOf({ id: 99, name: 'Ghost', value: 0 }).status).toBe('added');
		});

		it('counts derive from the memoised states', () => {
			const store = new EntityStore(makeConfig(), seed);
			store.addItem();
			store.patch(0, (draft) => (draft.value = 7));
			store.removeItem(1);

			expect(store.counts).toMatchObject({ added: 1, modified: 1, deleted: 1, total: 3 });
		});

		it("an unrelated record's state object is reference-stable across a patch (per-record cache hit)", () => {
			const store = new EntityStore(requiredNameConfig(), seed);
			const untouched = store.recordStates[0];

			store.patch(1, (draft) => (draft.value = 99));

			expect(store.recordStates[0]).toBe(untouched);
			expect(store.recordStates[1]).not.toBe(untouched);
		});

		it('removeItem flips a previously-cached record to deleted (cache invalidation without a reference change)', () => {
			const store = new EntityStore(requiredNameConfig(), seed);
			// Populate the memo for record 0 before its deleted-ness changes.
			expect(store.recordStates[0].status).toBe('clean');

			store.removeItem(0);

			expect(store.recordStates[0].status).toBe('deleted');
		});

		it('restoreItem flips a previously-cached deleted record back to clean', () => {
			const store = new EntityStore(requiredNameConfig(), seed);
			store.removeItem(0);
			expect(store.recordStates[0].status).toBe('deleted');

			store.restoreItem(0);

			expect(store.recordStates[0].status).toBe('clean');
		});
	});
});
