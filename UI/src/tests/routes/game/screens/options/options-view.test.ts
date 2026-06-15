import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ELogType, type ILogPreference } from '$lib/api';

// Mutable player-manager stand-in: `save()` reassigns `logPreferences`, so it is
// a plain writable property (not a getter). `vi.hoisted` keeps it initialised
// before the hoisted vi.mock factory runs.
const { mockPlayerManager, mockSendSocketCommand, toastError } = vi.hoisted(() => ({
	mockPlayerManager: { logPreferences: [] as ILogPreference[] },
	mockSendSocketCommand: vi.fn(),
	toastError: vi.fn()
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ toastError }));
vi.mock('$components', () => ({
	logColors: {
		player: '#c0d8ff',
		enemy: '#e8b6a6',
		loot: '#bde0b4',
		reward: '#f0d28a',
		system: 'rgba(240,240,240,0.7)'
	}
}));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	// `save()` persists through the WebSocket command; the spy each test configures
	// stands in for the live socket.
	return { ...actual, apiSocket: { sendSocketCommand: mockSendSocketCommand } };
});

import { OptionsView, LOG_TYPES } from '$routes/game/screens/options/options-view.svelte';

const allEnabled = (): ILogPreference[] => LOG_TYPES.map((lt) => ({ id: lt.id, enabled: true }));

let view: OptionsView;

beforeEach(() => {
	mockSendSocketCommand.mockReset().mockResolvedValue({});
	toastError.mockReset();
	mockPlayerManager.logPreferences = allEnabled();
	view = new OptionsView();
});

afterEach(() => view.dispose());

describe('OptionsView initialization', () => {
	it('seeds draft and baseline from the player preferences', () => {
		mockPlayerManager.logPreferences = [
			{ id: ELogType.Damage, enabled: false },
			{ id: ELogType.Debug, enabled: true }
		];
		view = new OptionsView();

		expect(view.isOn(ELogType.Damage)).toBe(false);
		expect(view.isOn(ELogType.Debug)).toBe(true);
		expect(view.isDirty).toBe(false);
	});

	it('defaults a missing log type to enabled', () => {
		mockPlayerManager.logPreferences = [{ id: ELogType.Damage, enabled: false }];
		view = new OptionsView();

		// Every type not present in the stored prefs falls back to enabled.
		expect(view.isOn(ELogType.Exp)).toBe(true);
		expect(view.enabledCount).toBe(LOG_TYPES.length - 1);
	});

	it('starts on the logging category', () => {
		expect(view.category).toBe('logging');
	});
});

describe('OptionsView dirty tracking', () => {
	it('marks a toggled type dirty and counts it', () => {
		view.setOne(ELogType.Damage, false);

		expect(view.isDirtyId(ELogType.Damage)).toBe(true);
		expect(view.dirtyIds).toEqual([ELogType.Damage]);
		expect(view.dirtyCount).toBe(1);
		expect(view.isDirty).toBe(true);
	});

	it('clears dirty when a type is toggled back to its baseline value', () => {
		view.setOne(ELogType.Damage, false);
		view.setOne(ELogType.Damage, true);

		expect(view.isDirty).toBe(false);
		expect(view.dirtyCount).toBe(0);
	});

	it('setMany toggles several types at once', () => {
		const combat = LOG_TYPES.filter((lt) => lt.group === 'combat').map((lt) => lt.id);
		view.setMany(combat, false);

		for (const id of combat) {
			expect(view.isOn(id)).toBe(false);
		}
		expect(view.dirtyCount).toBe(combat.length);
	});

	it('tracks the enabled count as toggles change', () => {
		const before = view.enabledCount;
		view.setOne(ELogType.Debug, false);
		expect(view.enabledCount).toBe(before - 1);
	});

	it('discard reverts the draft to the baseline', () => {
		view.setOne(ELogType.Damage, false);
		view.setOne(ELogType.Exp, false);
		view.discard();

		expect(view.isDirty).toBe(false);
		expect(view.isOn(ELogType.Damage)).toBe(true);
		expect(view.isOn(ELogType.Exp)).toBe(true);
	});
});

describe('OptionsView.changedPreferences', () => {
	it('returns only the changed preferences', () => {
		view.setOne(ELogType.Damage, false);
		view.setOne(ELogType.Debug, false);

		expect(view.changedPreferences).toEqual([
			{ id: ELogType.Damage, enabled: false },
			{ id: ELogType.Debug, enabled: false }
		]);
	});

	it('is empty when nothing has changed', () => {
		expect(view.changedPreferences).toEqual([]);
	});
});

describe('OptionsView.save', () => {
	it('sends only the changed preferences, applies them, and clears dirty on success', async () => {
		view.setOne(ELogType.Damage, false);
		await view.save();

		expect(mockSendSocketCommand).toHaveBeenCalledTimes(1);
		expect(mockSendSocketCommand).toHaveBeenCalledWith('SaveLogPreferences', [{ id: ELogType.Damage, enabled: false }]);
		// applied to the player manager so the live log filter reflects the change
		expect(mockPlayerManager.logPreferences.find((p) => p.id === ELogType.Damage)?.enabled).toBe(false);
		expect(view.isDirty).toBe(false);
		expect(view.saved).toBe(true);
		expect(toastError).not.toHaveBeenCalled();
	});

	it('keeps the change dirty and unapplied (so Save/Discard stay enabled) when the server rejects the save', async () => {
		mockSendSocketCommand.mockResolvedValue({ error: 'Unknown log type.' });
		view.setOne(ELogType.Exp, false);
		await view.save();

		// Not applied to the player and the baseline is unadvanced, so the change is
		// still dirty — the SaveBar enables Save/Discard on `isDirty`, allowing retry (#701).
		expect(mockPlayerManager.logPreferences.find((p) => p.id === ELogType.Exp)?.enabled).toBe(true);
		expect(view.isDirty).toBe(true);
		expect(view.dirtyCount).toBe(1);
		expect(view.saving).toBe(false);
		expect(view.saved).toBe(false);
		expect(toastError).toHaveBeenCalledTimes(1);
	});

	it('retries successfully after a failed save (the change survived the failure)', async () => {
		mockSendSocketCommand.mockResolvedValueOnce({ error: 'transient' });
		view.setOne(ELogType.Exp, false);
		await view.save();
		expect(view.isDirty).toBe(true);

		// A second attempt resolves cleanly and commits the still-dirty change.
		mockSendSocketCommand.mockResolvedValueOnce({});
		await view.save();

		expect(mockSendSocketCommand).toHaveBeenCalledTimes(2);
		expect(mockPlayerManager.logPreferences.find((p) => p.id === ELogType.Exp)?.enabled).toBe(false);
		expect(view.isDirty).toBe(false);
		expect(view.saved).toBe(true);
	});

	it('does nothing when there are no changes', async () => {
		await view.save();

		expect(mockSendSocketCommand).not.toHaveBeenCalled();
		expect(toastError).not.toHaveBeenCalled();
	});

	it('toggling after a save clears the saved flash', async () => {
		view.setOne(ELogType.Damage, false);
		await view.save();
		expect(view.saved).toBe(true);

		view.setOne(ELogType.Damage, true);
		expect(view.saved).toBe(false);
	});
});
