import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { ELogType, type ILogPreference } from '$lib/api';

// Mutable player-manager stand-in: `save()` reassigns `logPreferences`, and the view reads it
// live (not just at construction, #1809), so it must be reactive like the real (statified)
// PlayerManager — declared as a class instance (not an object literal) so `statify` applies.
// `vi.hoisted` keeps it initialised before the hoisted vi.mock factory runs.
const { mockPlayerManager, mockSendSocketCommand, toastError } = vi.hoisted(() => ({
	mockPlayerManager: new (class MockPlayerManager {
		logPreferences: ILogPreference[] = [];
	})(),
	mockSendSocketCommand: vi.fn(),
	toastError: vi.fn()
}));

vi.mock('$lib/engine', async () => {
	const { statify } = await import('$lib/common');
	return { playerManager: statify(mockPlayerManager) };
});
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

	it('keeps a toggle flipped while the save is in flight dirty instead of baselining it (#1506)', async () => {
		let resolveSend: (value: unknown) => void = () => {};
		mockSendSocketCommand.mockReturnValue(new Promise((resolve) => (resolveSend = resolve)));

		view.setOne(ELogType.Damage, false);
		const saving = view.save();
		// Flipped while the request is in flight — not part of the sent diff.
		view.setOne(ELogType.Exp, false);
		resolveSend({});
		await saving;

		// The sent entry is committed to the baseline and the player manager...
		expect(view.isDirtyId(ELogType.Damage)).toBe(false);
		expect(mockPlayerManager.logPreferences.find((p) => p.id === ELogType.Damage)?.enabled).toBe(false);
		// ...but the mid-flight flip stays dirty and unapplied, so it can still be saved.
		expect(view.isDirtyId(ELogType.Exp)).toBe(true);
		expect(view.isDirty).toBe(true);
		expect(mockPlayerManager.logPreferences.find((p) => p.id === ELogType.Exp)?.enabled).toBe(true);

		// A follow-up save sends exactly the still-dirty entry.
		mockSendSocketCommand.mockResolvedValue({});
		await view.save();
		expect(mockSendSocketCommand).toHaveBeenLastCalledWith('SaveLogPreferences', [
			{ id: ELogType.Exp, enabled: false }
		]);
		expect(view.isDirty).toBe(false);
	});

	it('keeps a sent toggle that was flipped back mid-flight dirty at its new value', async () => {
		let resolveSend: (value: unknown) => void = () => {};
		mockSendSocketCommand.mockReturnValue(new Promise((resolve) => (resolveSend = resolve)));

		view.setOne(ELogType.Damage, false);
		const saving = view.save();
		// The same type flips back while its disable is in flight.
		view.setOne(ELogType.Damage, true);
		resolveSend({});
		await saving;

		// The server now holds the sent value (false), so the flip back reads as a new
		// pending change against that baseline rather than being lost.
		expect(mockPlayerManager.logPreferences.find((p) => p.id === ELogType.Damage)?.enabled).toBe(false);
		expect(view.isOn(ELogType.Damage)).toBe(true);
		expect(view.isDirtyId(ELogType.Damage)).toBe(true);
		expect(view.changedPreferences).toEqual([{ id: ELogType.Damage, enabled: true }]);
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

describe('OptionsView mid-session resync (#1809)', () => {
	it('follows a resync of the underlying preferences while idle instead of going stale', () => {
		expect(view.isOn(ELogType.Damage)).toBe(true);

		// A background resync (e.g. playerManager.initialize() after a dead-lettered event)
		// reassigns logPreferences wholesale, with no interaction from this screen.
		mockPlayerManager.logPreferences = [{ id: ELogType.Damage, enabled: false }];

		expect(view.isOn(ELogType.Damage)).toBe(false);
		expect(view.isDirty).toBe(false);
	});

	it('clears a stale dirty state when a resync reveals a lost save actually applied', () => {
		view.setOne(ELogType.Damage, false);
		expect(view.isDirty).toBe(true);

		// A later background resync reveals the server actually holds the toggled-off value.
		mockPlayerManager.logPreferences = [{ id: ELogType.Damage, enabled: false }];

		expect(view.isOn(ELogType.Damage)).toBe(false);
		expect(view.isDirty).toBe(false);
	});

	it('discards an in-progress unsaved toggle when an external resync arrives, so a stale diff is never sent', () => {
		view.setOne(ELogType.Damage, false); // unsaved local edit
		expect(view.isDirty).toBe(true);

		// An external resync (unrelated to this toggle, and not this view's own save) reassigns
		// logPreferences wholesale. The now-unreliable pending toggle can't be trusted to still
		// apply cleanly against a baseline it was never computed against, so it is discarded
		// outright rather than layered onto the fresh baseline (#1809).
		mockPlayerManager.logPreferences = [{ id: ELogType.Exp, enabled: false }];

		expect(view.isOn(ELogType.Exp)).toBe(false);
		expect(view.isOn(ELogType.Damage)).toBe(true); // back to the (defaulted-enabled) baseline
		expect(view.isDirty).toBe(false);
		expect(view.changedPreferences).toEqual([]);
	});
});
