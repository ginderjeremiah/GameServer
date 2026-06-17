import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ELogType } from '$lib/api';

const { addLog, mockPlayerManager } = vi.hoisted(() => {
	const addLog = vi.fn();
	// Mirrors PlayerManager's prebuilt map: an unknown type defaults to enabled.
	const mockPlayerManager = {
		logPreferences: [] as { id: number; enabled: boolean }[],
		logTypeEnabled(logType: number): boolean {
			return mockPlayerManager.logPreferences.find((pref) => pref.id === logType)?.enabled ?? true;
		}
	};
	return { addLog, mockPlayerManager };
});

vi.mock('$lib/engine/player/player-manager', () => ({
	get playerManager() {
		return mockPlayerManager;
	}
}));

// logMessage only gates on the player's preferences and delegates the append to the logs store.
vi.mock('$stores', () => ({ addLog }));

import { logMessage } from '$lib/engine/log';

describe('logMessage', () => {
	beforeEach(() => {
		addLog.mockClear();
		mockPlayerManager.logPreferences = [
			{ id: ELogType.Damage, enabled: false },
			{ id: ELogType.Exp, enabled: true },
			{ id: ELogType.LevelUp, enabled: true },
			{ id: ELogType.Debug, enabled: false }
		];
	});

	it('forwards an enabled log type to the store (no outcome by default)', () => {
		logMessage(ELogType.Exp, 'Earned 50 exp.');

		expect(addLog).toHaveBeenCalledWith(ELogType.Exp, 'Earned 50 exp.', undefined);
	});

	it('forwards a structured outcome through to the store', () => {
		mockPlayerManager.logPreferences = [{ id: ELogType.Damage, enabled: true }];
		logMessage(ELogType.Damage, 'You used Slash and dealt 10 damage!', 'player-hit');

		expect(addLog).toHaveBeenCalledWith(ELogType.Damage, 'You used Slash and dealt 10 damage!', 'player-hit');
	});

	it('does not forward a disabled log type', () => {
		logMessage(ELogType.Damage, 'Hit for 10 damage.');

		expect(addLog).not.toHaveBeenCalled();
	});

	it('forwards an unknown log type (defaults to enabled)', () => {
		logMessage(ELogType.EnemyDefeated, 'Enemy defeated!');

		expect(addLog).toHaveBeenCalledWith(ELogType.EnemyDefeated, 'Enemy defeated!', undefined);
	});
});
