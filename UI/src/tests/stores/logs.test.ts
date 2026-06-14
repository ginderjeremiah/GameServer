import { describe, it, expect, beforeEach } from 'vitest';
import { ELogType } from '$lib/api';
import { logs, addLog, resetLogs } from '$stores/logs.svelte';

describe('logs store', () => {
	beforeEach(() => {
		resetLogs();
	});

	it('prepends new entries newest-first', () => {
		addLog(ELogType.Exp, 'First');
		addLog(ELogType.Exp, 'Second');

		expect(logs().map((l) => l.message)).toEqual(['Second', 'First']);
	});

	it('assigns incrementing ids', () => {
		addLog(ELogType.Exp, 'A');
		addLog(ELogType.LevelUp, 'B');

		// Newest-first, so index 0 (the latest) holds the higher id.
		expect(logs()[0].id).toBeGreaterThan(logs()[1].id);
	});

	it('caps the list at 40 entries, dropping the oldest', () => {
		for (let i = 0; i < 45; i++) {
			addLog(ELogType.Exp, `Message ${i}`);
		}

		expect(logs()).toHaveLength(40);
		expect(logs()[0].message).toBe('Message 44'); // newest kept
		expect(logs()[39].message).toBe('Message 5'); // oldest five dropped
	});

	it('resetLogs clears the entries so a prior session does not leak into the next', () => {
		addLog(ELogType.Exp, 'Stale');
		expect(logs()).toHaveLength(1);

		resetLogs();

		expect(logs()).toHaveLength(0);
	});

	it('resetLogs resets the id counter so ids restart from 1', () => {
		addLog(ELogType.Exp, 'A');
		addLog(ELogType.Exp, 'B');
		expect(logs()[0].id).toBe(2);

		resetLogs();
		addLog(ELogType.Exp, 'C');

		expect(logs()[0].id).toBe(1);
	});
});
