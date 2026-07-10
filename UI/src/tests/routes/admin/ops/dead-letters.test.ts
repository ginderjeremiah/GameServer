import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { IDeadLetterEntry, IDeadLetterInspection, IDeadLetterReplayResult } from '$lib/api';

const { getMock, postMock } = vi.hoisted(() => ({ getMock: vi.fn(), postMock: vi.fn() }));

// Keep the real EDeadLetterReason enum (a pure types module) and only swap the HTTP transport.
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: getMock, post: postMock } };
});

import { EDeadLetterReason } from '$lib/api';
import {
	DeadLetterConsoleState,
	DEAD_LETTER_PAGE_SIZE,
	formatPayload,
	reasonMeta
} from '$routes/admin/ops/dead-letters.svelte';

const entry = (overrides: Partial<IDeadLetterEntry> & Pick<IDeadLetterEntry, 'index'>): IDeadLetterEntry => ({
	eventType: 'PlayerLeveledEvent',
	playerId: 7,
	reason: EDeadLetterReason.Replayable,
	rawPayload: `{"index":${overrides.index}}`,
	...overrides
});

const inspection = (entries: IDeadLetterEntry[], total = entries.length): IDeadLetterInspection => ({
	totalCount: total,
	entries
});

const replayResult = (replayed: number, remaining: number): IDeadLetterReplayResult => ({
	replayedCount: replayed,
	remainingCount: remaining
});

beforeEach(() => {
	getMock.mockReset();
	postMock.mockReset();
});

describe('reasonMeta', () => {
	it('classifies malformed as non-replayable poison', () => {
		const meta = reasonMeta(EDeadLetterReason.Malformed);
		expect(meta.replayable).toBe(false);
		expect(meta.tone).toBe('poison');
		expect(meta.label).toBe('Malformed');
	});

	it('classifies unknown event types as non-replayable', () => {
		const meta = reasonMeta(EDeadLetterReason.UnknownEventType);
		expect(meta.replayable).toBe(false);
		expect(meta.tone).toBe('warn');
	});

	it('classifies replayable entries as replayable', () => {
		const meta = reasonMeta(EDeadLetterReason.Replayable);
		expect(meta.replayable).toBe(true);
		expect(meta.tone).toBe('ok');
		expect(meta.label).toBe('Replayable');
	});
});

describe('formatPayload', () => {
	it('pretty-prints valid JSON', () => {
		expect(formatPayload('{"a":1,"b":2}')).toBe('{\n  "a": 1,\n  "b": 2\n}');
	});

	it('returns the raw string verbatim when it is not valid JSON', () => {
		expect(formatPayload('not json {')).toBe('not json {');
	});
});

describe('DeadLetterConsoleState.load', () => {
	it('requests the capped page, populates the snapshot, and flips loaded', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 }), entry({ index: 1 })], 5));
		const state = new DeadLetterConsoleState();

		const ok = await state.load();

		expect(ok).toBe(true);
		expect(getMock).toHaveBeenCalledWith('AdminTools/GetPlayerUpdateDeadLetters', { max: DEAD_LETTER_PAGE_SIZE });
		expect(state.totalCount).toBe(5);
		expect(state.entries.length).toBe(2);
		expect(state.loaded).toBe(true);
		expect(state.loading).toBe(false);
		expect(state.hasMore).toBe(true);
		expect(state.error).toBeNull();
	});

	it('sets an error and returns false when the request fails', async () => {
		getMock.mockRejectedValue(new Error('boom'));
		const state = new DeadLetterConsoleState();

		const ok = await state.load();

		expect(ok).toBe(false);
		expect(state.error).toBe('boom');
		expect(state.loading).toBe(false);
		expect(state.loaded).toBe(false);
	});

	it('clears a stale selection when reloading', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 }), entry({ index: 1 })]));
		const state = new DeadLetterConsoleState();
		await state.load();
		state.toggle(0);
		expect(state.selectedCount).toBe(1);

		await state.load();

		expect(state.selectedCount).toBe(0);
	});

	it('bumps generation on every successful load, but not on a failed one', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 })]));
		const state = new DeadLetterConsoleState();
		expect(state.generation).toBe(0);

		await state.load();
		expect(state.generation).toBe(1);

		getMock.mockRejectedValueOnce(new Error('boom'));
		await state.load();
		expect(state.generation).toBe(1);

		await state.load();
		expect(state.generation).toBe(2);
	});
});

describe('DeadLetterConsoleState selection', () => {
	const seed = async () => {
		getMock.mockResolvedValue(
			inspection([
				entry({ index: 0, reason: EDeadLetterReason.Replayable }),
				entry({ index: 1, reason: EDeadLetterReason.Malformed, eventType: undefined, rawPayload: 'dup' }),
				entry({ index: 2, reason: EDeadLetterReason.UnknownEventType, rawPayload: 'dup' })
			])
		);
		const state = new DeadLetterConsoleState();
		await state.load();
		return state;
	};

	it('toggles individual entries on and off', async () => {
		const state = await seed();
		state.toggle(1);
		expect(state.isSelected(1)).toBe(true);
		state.toggle(1);
		expect(state.isSelected(1)).toBe(false);
	});

	it('selects and clears all visible entries', async () => {
		const state = await seed();
		state.setAllVisible(true);
		expect(state.allVisibleSelected).toBe(true);
		expect(state.selectedCount).toBe(3);
		state.setAllVisible(false);
		expect(state.selectedCount).toBe(0);
		expect(state.allVisibleSelected).toBe(false);
	});

	it('preserves duplicate payload multiplicity in the selected payloads', async () => {
		const state = await seed();
		// Entries 1 and 2 share the raw payload "dup"; selecting both must yield two copies so the
		// backend replays both queued occurrences, not just one.
		state.toggle(1);
		state.toggle(2);
		expect(state.selectedPayloads).toEqual(['dup', 'dup']);
	});

	it('counts non-replayable entries among the selection', async () => {
		const state = await seed();
		state.setAllVisible(true);
		// Index 1 (malformed) and index 2 (unknown) are poison; index 0 (replayable) is not.
		expect(state.nonReplayableSelectedCount).toBe(2);
	});
});

describe('DeadLetterConsoleState.replay', () => {
	it('replays the selection by payload and refreshes', async () => {
		getMock.mockResolvedValue(
			inspection([entry({ index: 0, rawPayload: 'p0' }), entry({ index: 1, rawPayload: 'p1' })])
		);
		postMock.mockResolvedValue(replayResult(1, 1));
		const state = new DeadLetterConsoleState();
		await state.load();
		state.toggle(0);
		getMock.mockClear();

		const result = await state.replay('selected');

		expect(postMock).toHaveBeenCalledWith('AdminTools/ReplayPlayerUpdateDeadLetters', {
			all: false,
			payloads: ['p0']
		});
		expect(result).toEqual(replayResult(1, 1));
		// The view refetches afterwards.
		expect(getMock).toHaveBeenCalledTimes(1);
		expect(state.replaying).toBe(false);
	});

	it('replays everything with all=true and no payloads', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 })], 1));
		postMock.mockResolvedValue(replayResult(1, 0));
		const state = new DeadLetterConsoleState();
		await state.load();

		await state.replay('all');

		expect(postMock).toHaveBeenCalledWith('AdminTools/ReplayPlayerUpdateDeadLetters', {
			all: true,
			payloads: undefined
		});
	});

	it('returns the success result and swallows a failed post-replay refresh', async () => {
		// The post succeeds but the subsequent refresh inspect fails. The replay genuinely happened,
		// so the result is returned and the blocking error panel is left clear (operator can Refresh).
		getMock.mockResolvedValueOnce(inspection([entry({ index: 0 })])).mockRejectedValueOnce(new Error('refresh down'));
		postMock.mockResolvedValue(replayResult(2, 0));
		const state = new DeadLetterConsoleState();
		await state.load();

		const result = await state.replay('all');

		expect(result).toEqual(replayResult(2, 0));
		expect(state.error).toBeNull();
		expect(state.replaying).toBe(false);
	});

	it('sets an error and returns null when replay fails', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 })]));
		postMock.mockRejectedValue(new Error('replay failed'));
		const state = new DeadLetterConsoleState();
		await state.load();

		const result = await state.replay('all');

		expect(result).toBeNull();
		expect(state.error).toBe('replay failed');
		expect(state.replaying).toBe(false);
	});
});

describe('DeadLetterConsoleState (socket-command variant)', () => {
	it('inspects and replays against the socket command routes instead of the player-update ones', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0, rawPayload: 'p0' })], 1));
		postMock.mockResolvedValue(replayResult(1, 0));
		const state = new DeadLetterConsoleState('socket-command');

		await state.load();
		expect(getMock).toHaveBeenCalledWith('AdminTools/GetSocketCommandDeadLetters', {
			max: DEAD_LETTER_PAGE_SIZE
		});

		await state.replay('all');
		expect(postMock).toHaveBeenCalledWith('AdminTools/ReplaySocketCommandDeadLetters', {
			all: true,
			payloads: undefined
		});
	});
});
