import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ELogType } from '$lib/api';

const { mockLogs, mockPlayerManager } = vi.hoisted(() => {
	const mockLogs: { id: number; logType: number; message: string }[] = [];
	// Mirrors PlayerManager's prebuilt map: an unknown type defaults to enabled.
	const mockPlayerManager = {
		logPreferences: [] as { id: number; enabled: boolean }[],
		logTypeEnabled(logType: number): boolean {
			return mockPlayerManager.logPreferences.find((pref) => pref.id === logType)?.enabled ?? true;
		}
	};
	return { mockLogs, mockPlayerManager };
});

vi.mock('$lib/engine/player/player-manager', () => ({
	get playerManager() {
		return mockPlayerManager;
	}
}));

vi.mock('$stores', () => ({
	logs: () => mockLogs
}));

import { logMessage } from '$lib/engine/log';

describe('logMessage', () => {
	beforeEach(() => {
		mockLogs.length = 0;
		mockPlayerManager.logPreferences = [
			{ id: ELogType.Damage, enabled: false },
			{ id: ELogType.Exp, enabled: true },
			{ id: ELogType.LevelUp, enabled: true },
			{ id: ELogType.Debug, enabled: false }
		];
	});

	it('adds message for enabled log type', () => {
		logMessage(ELogType.Exp, 'Earned 50 exp.');

		expect(mockLogs.length).toBe(1);
		expect(mockLogs[0].logType).toBe(ELogType.Exp);
		expect(mockLogs[0].message).toBe('Earned 50 exp.');
	});

	it('does not add message for disabled log type', () => {
		logMessage(ELogType.Damage, 'Hit for 10 damage.');

		expect(mockLogs.length).toBe(0);
	});

	it('adds message for unknown log type (defaults to enabled)', () => {
		logMessage(ELogType.EnemyDefeated, 'Enemy defeated!');

		expect(mockLogs.length).toBe(1);
		expect(mockLogs[0].message).toBe('Enemy defeated!');
	});

	it('prepends new messages to the front', () => {
		logMessage(ELogType.Exp, 'First');
		logMessage(ELogType.Exp, 'Second');

		expect(mockLogs[0].message).toBe('Second');
		expect(mockLogs[1].message).toBe('First');
	});

	it('assigns incrementing ids', () => {
		logMessage(ELogType.Exp, 'A');
		logMessage(ELogType.LevelUp, 'B');

		expect(mockLogs[0].id).toBeGreaterThan(mockLogs[1].id);
	});

	it('caps log list at 40 entries', () => {
		for (let i = 0; i < 45; i++) {
			logMessage(ELogType.Exp, `Message ${i}`);
		}

		expect(mockLogs.length).toBeLessThanOrEqual(40);
	});
});
