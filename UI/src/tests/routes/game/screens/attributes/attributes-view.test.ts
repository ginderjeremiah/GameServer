import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { EAttribute, type IBattlerAttribute } from '$lib/api';

// Mutable player-manager stand-in: `save()` reassigns `attributes` and adopts the
// server's `statPointsUsed` absolutely. Declared as a class instance (not an object
// literal) so `statify` can make its fields reactive — the view derives the
// stat-point pool from them live, exactly like the real (statified) PlayerManager.
const { mockPlayerManager, sendSocketCommand, toastError, staticData } = vi.hoisted(() => ({
	mockPlayerManager: new (class MockPlayerManager {
		attributes: IBattlerAttribute[] = [];
		statPointsGained = 0;
		statPointsUsed = 0;
	})(),
	sendSocketCommand: vi.fn(),
	toastError: vi.fn(),
	// Reference data is intentionally absent so the view falls back to enum names.
	staticData: { attributes: undefined as unknown }
}));

vi.mock('$lib/engine', async () => {
	const { statify } = await import('$lib/common');
	return { playerManager: statify(mockPlayerManager) };
});
vi.mock('$stores', () => ({ staticData, toastError }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});

import { attributeName } from '$lib/common';
import {
	AttributesView,
	CORE_ATTRIBUTES,
	DERIVED_STATS,
	deriveStats,
	perPointYields,
	feedsFor,
	contributorsFor,
	derivedShortLabel,
	radarValueAtPointer
} from '$routes/game/screens/attributes/attributes-view.svelte';

const allFives = (): IBattlerAttribute[] => CORE_ATTRIBUTES.map((id) => ({ attributeId: id, amount: 5 }));

const idx = {
	str: CORE_ATTRIBUTES.indexOf(EAttribute.Strength),
	end: CORE_ATTRIBUTES.indexOf(EAttribute.Endurance),
	int: CORE_ATTRIBUTES.indexOf(EAttribute.Intellect),
	agi: CORE_ATTRIBUTES.indexOf(EAttribute.Agility),
	dex: CORE_ATTRIBUTES.indexOf(EAttribute.Dexterity),
	luk: CORE_ATTRIBUTES.indexOf(EAttribute.Luck)
};

let view: AttributesView;

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({ data: { attributes: allFives(), statPointsUsed: 0 } });
	toastError.mockReset();
	localStorage.clear();
	mockPlayerManager.attributes = allFives();
	mockPlayerManager.statPointsGained = 10;
	mockPlayerManager.statPointsUsed = 0;
	view = new AttributesView();
});

afterEach(() => view.dispose());

/* ── pure derived-stat helpers (the real combat formulas) ─────────────────── */

describe('deriveStats', () => {
	it('matches the real BattleAttributes formulas for a zero build', () => {
		const d = deriveStats([0, 0, 0, 0, 0, 0]);
		expect(d[EAttribute.MaxHealth]).toBe(50);
		expect(d[EAttribute.Toughness]).toBe(0);
		expect(d[EAttribute.CooldownRecovery]).toBe(1);
	});

	it('computes MaxHealth = 50 + 20*END + 5*STR', () => {
		const values = [...CORE_ATTRIBUTES.map(() => 0)];
		values[idx.str] = 10;
		values[idx.end] = 20;
		expect(deriveStats(values)[EAttribute.MaxHealth]).toBe(50 + 20 * 20 + 5 * 10);
	});

	it('only surfaces the derived stats the game actually computes', () => {
		// The deprecated DropBonus, the authored-only DamageReflection, and the crit/dodge set are
		// intentionally excluded.
		const ids = DERIVED_STATS.map((d) => d.id);
		expect(ids).toEqual([EAttribute.MaxHealth, EAttribute.Toughness, EAttribute.CooldownRecovery]);
		expect(ids).not.toContain(EAttribute.DropBonus);
		expect(ids).not.toContain(EAttribute.CriticalChanceMultiplier);
		expect(ids).not.toContain(EAttribute.DamageReflection);
	});
});

describe('perPointYields', () => {
	it('STR yields +5 Max Health per point', () => {
		expect(perPointYields(idx.str)).toEqual([{ id: EAttribute.MaxHealth, delta: 5 }]);
	});

	it('END yields +20 Max Health and +2 Toughness per point', () => {
		expect(perPointYields(idx.end)).toEqual([
			{ id: EAttribute.MaxHealth, delta: 20 },
			{ id: EAttribute.Toughness, delta: 2 }
		]);
	});

	// CDR was severed from the core attributes (#1426): Agility now feeds only the opt-in cadence/evasion
	// multipliers (inert without an enabler, not surfaced in DERIVED_STATS) and Dexterity shed its CDR sliver,
	// so neither yields a surfaced derived stat any longer.
	it('AGI has no surfaced derived yield (its cadence/evasion multipliers are opt-in)', () => {
		expect(perPointYields(idx.agi)).toEqual([]);
	});

	it('DEX has no surfaced derived yield (its Cooldown Recovery sliver was severed, #1426)', () => {
		expect(perPointYields(idx.dex)).toEqual([]);
	});

	it('INT and LUK have no surfaced derived yield yet', () => {
		expect(perPointYields(idx.int)).toEqual([]);
		expect(perPointYields(idx.luk)).toEqual([]);
	});

	it('is constant per index — the marginal yield does not depend on the current allocation', () => {
		// The surfaced derived stats are linear (purely additive modifiers), so a point's marginal
		// yield is invariant to how many points are already allocated. This is what lets the per-point
		// line be memoised once instead of re-derived on every stepper click / radar drag.
		expect(perPointYields(idx.end)).toEqual(perPointYields(idx.end));
		const allocations = [
			[0, 0, 0, 0, 0, 0],
			[5, 5, 5, 5, 5, 5],
			[99, 3, 17, 42, 8, 61]
		];
		for (const values of allocations) {
			for (let i = 0; i < CORE_ATTRIBUTES.length; i++) {
				const bumped = [...values];
				bumped[i] += 1;
				const before = deriveStats(values);
				const after = deriveStats(bumped);
				const expected = DERIVED_STATS.flatMap((def) => {
					const delta = Math.round((after[def.id] - before[def.id]) * 1e6) / 1e6;
					return delta !== 0 ? [{ id: def.id, delta }] : [];
				});
				expect(perPointYields(i)).toEqual(expected);
			}
		}
	});

	it('returns the same memoised array reference across calls (no per-call rebuild)', () => {
		expect(perPointYields(idx.agi)).toBe(perPointYields(idx.agi));
	});
});

describe('feedsFor', () => {
	it('reports the derived stats each attribute drives', () => {
		expect(feedsFor(idx.str)).toEqual([EAttribute.MaxHealth]);
		expect(feedsFor(idx.end)).toEqual([EAttribute.MaxHealth, EAttribute.Toughness]);
		// CDR is severed from the core attributes (#1426): Agility now feeds only the opt-in cadence/evasion
		// multipliers (not surfaced here, inert without an enabler) and Dexterity feeds nothing surfaced, so
		// neither drives a stat in the DERIVED_STATS panel any longer.
		expect(feedsFor(idx.agi)).toEqual([]);
		expect(feedsFor(idx.dex)).toEqual([]);
		expect(feedsFor(idx.int)).toEqual([]);
		expect(feedsFor(idx.luk)).toEqual([]);
	});
});

describe('contributorsFor', () => {
	it('reports the core attributes feeding each derived stat (inverse of feedsFor)', () => {
		expect(contributorsFor(EAttribute.MaxHealth)).toEqual([EAttribute.Strength, EAttribute.Endurance]);
		expect(contributorsFor(EAttribute.Toughness)).toEqual([EAttribute.Endurance]);
	});

	it('returns no contributors for an attribute no core stat derives', () => {
		expect(contributorsFor(EAttribute.Strength)).toEqual([]);
		expect(contributorsFor(EAttribute.Luck)).toEqual([]);
		// CDR keeps its base 1.0 but is no longer fed by any core attribute (#1426).
		expect(contributorsFor(EAttribute.CooldownRecovery)).toEqual([]);
	});
});

describe('radarValueAtPointer', () => {
	// Geometry used by the guided radar: centre 215, axis length 155, scale 20.
	const C = 215;
	const R = 155;
	const hexMax = 20;
	// The first axis (Strength) points straight up: angle -90°.
	const upAxis = (-90 * Math.PI) / 180;

	it('maps a half-extent radial drag to half the radar scale', () => {
		// Half of R out from centre along the "up" axis (smaller y) → half of hexMax.
		expect(radarValueAtPointer(C, C - R / 2, C, upAxis, R, hexMax)).toBeCloseTo(hexMax / 2, 5);
	});

	it('maps the centre to zero and the rim to the full scale', () => {
		expect(radarValueAtPointer(C, C, C, upAxis, R, hexMax)).toBeCloseTo(0, 5);
		expect(radarValueAtPointer(C, C - R, C, upAxis, R, hexMax)).toBeCloseTo(hexMax, 5);
	});

	it('ignores perpendicular drift by projecting onto the axis', () => {
		// Sideways offset on the up axis must not change the radial value.
		const straight = radarValueAtPointer(C, C - R / 2, C, upAxis, R, hexMax);
		const drifted = radarValueAtPointer(C + 60, C - R / 2, C, upAxis, R, hexMax);
		expect(drifted).toBeCloseTo(straight, 5);
	});

	it('returns an unclamped value past the rim (the view-model clamps)', () => {
		expect(radarValueAtPointer(C, C - R * 1.5, C, upAxis, R, hexMax)).toBeCloseTo(hexMax * 1.5, 5);
	});

	it('guards against a zero-length axis', () => {
		expect(radarValueAtPointer(C, C, C, upAxis, 0, hexMax)).toBe(0);
	});
});

describe('label helpers', () => {
	it('provides compact labels for derived stats', () => {
		expect(derivedShortLabel(EAttribute.MaxHealth)).toBe('HP');
		expect(derivedShortLabel(EAttribute.CooldownRecovery)).toBe('CDR');
	});

	it('falls back to a normalised enum name when reference data is absent', () => {
		expect(attributeName(EAttribute.MaxHealth)).toBe('Max Health');
		expect(attributeName(EAttribute.Strength)).toBe('Strength');
	});
});

/* ── AttributesView allocation model ──────────────────────────────────────── */

describe('AttributesView initialization', () => {
	it('seeds the baseline and budget from the player manager', () => {
		expect(view.committed).toEqual([5, 5, 5, 5, 5, 5]);
		expect(view.values).toEqual([5, 5, 5, 5, 5, 5]);
		expect(view.savedAvailable).toBe(10);
		expect(view.remaining).toBe(10);
		expect(view.dirty).toBe(false);
	});

	it('defaults a missing allocation to 0', () => {
		mockPlayerManager.attributes = [{ attributeId: EAttribute.Strength, amount: 7 }];
		view = new AttributesView();
		expect(view.committed[idx.str]).toBe(7);
		expect(view.committed[idx.luk]).toBe(0);
	});
});

describe('AttributesView allocation', () => {
	it('increments an attribute and spends a point', () => {
		view.inc(idx.str);
		expect(view.values[idx.str]).toBe(6);
		expect(view.remaining).toBe(9);
		expect(view.dirty).toBe(true);
		expect(view.changedCount).toBe(1);
	});

	it('blocks incrementing once the pool is empty', () => {
		for (let n = 0; n < 10; n++) {
			view.inc(idx.str);
		}
		expect(view.remaining).toBe(0);
		expect(view.canInc()).toBe(false);
		view.inc(idx.str);
		expect(view.values[idx.str]).toBe(15); // unchanged by the blocked 11th point
	});

	it('allows refunding committed points down to zero (free reallocation)', () => {
		// The backend permits moving points freely, so any attribute can be
		// decremented below its saved value, refunding to the pool.
		view.dec(idx.str);
		expect(view.values[idx.str]).toBe(4);
		expect(view.remaining).toBe(11);

		for (let n = 0; n < 4; n++) {
			view.dec(idx.str);
		}
		expect(view.values[idx.str]).toBe(0);
		expect(view.canDec(idx.str)).toBe(false);
		view.dec(idx.str);
		expect(view.values[idx.str]).toBe(0);
	});

	it('sets an attribute directly to a target value (drag)', () => {
		view.setValue(idx.str, 8);
		expect(view.values[idx.str]).toBe(8);
		expect(view.remaining).toBe(7);

		view.setValue(idx.str, 2);
		expect(view.values[idx.str]).toBe(2);
		expect(view.remaining).toBe(13);
	});

	it('clamps setValue to the remaining budget when increasing', () => {
		view.setValue(idx.str, 100);
		// Only the 10 available points can be spent on top of the saved 5.
		expect(view.values[idx.str]).toBe(15);
		expect(view.remaining).toBe(0);
	});

	it('clamps setValue to zero (and rounds) when decreasing', () => {
		view.setValue(idx.str, -5);
		expect(view.values[idx.str]).toBe(0);
		view.setValue(idx.end, 4.7);
		expect(view.values[idx.end]).toBe(5); // rounds to nearest, unchanged from baseline
		expect(view.dirty).toBe(true); // STR refund still pending
	});

	it('setValue is a no-op when the target equals the current value', () => {
		view.setValue(idx.str, 5);
		expect(view.dirty).toBe(false);
		expect(view.values).toEqual([5, 5, 5, 5, 5, 5]);
	});

	it('discard reverts the draft to the baseline', () => {
		view.inc(idx.str);
		view.dec(idx.end);
		view.discard();
		expect(view.values).toEqual([5, 5, 5, 5, 5, 5]);
		expect(view.dirty).toBe(false);
	});

	it('computes only the changed per-attribute deltas', () => {
		view.inc(idx.str);
		view.inc(idx.str);
		view.dec(idx.agi);
		expect(view.changedUpdates).toEqual([
			{ attributeId: EAttribute.Strength, amount: 2 },
			{ attributeId: EAttribute.Agility, amount: -1 }
		]);
	});

	it('previews derived stats for the pending build', () => {
		view.inc(idx.end); // +20 Max Health
		expect(view.derived[EAttribute.MaxHealth] - view.savedDerived[EAttribute.MaxHealth]).toBe(20);
	});
});

describe('AttributesView radar scale lock', () => {
	// The radar maps a pointer position to a value through hexMax, so while a drag
	// is in progress the scale must stay fixed — otherwise allocating points would
	// rescale the radar and inflate the value under the pointer (#433).
	it('pins hexMax to its lock-time value even as the draft grows', () => {
		expect(view.hexMax).toBe(10); // peak 5 → 10
		view.lockScale();
		// Spend the whole pool on one axis; the unpinned scale would jump to 20.
		view.setValue(idx.str, 15);
		expect(view.values[idx.str]).toBe(15);
		expect(view.hexMax).toBe(10); // still pinned
	});

	it('recomputes hexMax from the allocation once unlocked', () => {
		view.lockScale();
		view.setValue(idx.str, 15);
		view.unlockScale();
		expect(view.hexMax).toBe(20); // peak 15 → ceil(18/5)*5
	});

	it('captures the current scale at lock time, not a stale one', () => {
		view.setValue(idx.str, 9); // peak 9 → 15 while unpinned
		expect(view.hexMax).toBe(15);
		view.lockScale();
		view.setValue(idx.str, 5); // refund back down; the scale stays pinned at 15
		expect(view.hexMax).toBe(15);
		view.unlockScale();
		expect(view.hexMax).toBe(10); // peak 5 again
	});
});

describe('AttributesView mode persistence', () => {
	it('defaults to guided and persists the chosen mode', () => {
		expect(view.mode).toBe('guided');
		view.setMode('theory');
		expect(view.mode).toBe('theory');
		expect(localStorage.getItem('ttf.attr.mode')).toBe('theory');

		const reopened = new AttributesView();
		expect(reopened.mode).toBe('theory');
		reopened.dispose();
	});
});

describe('AttributesView.save', () => {
	it('sends the deltas over the socket, applies the result, and clears dirty on success', async () => {
		const serverResult: IBattlerAttribute[] = CORE_ATTRIBUTES.map((id) => ({
			attributeId: id,
			amount: id === EAttribute.Strength ? 6 : 5
		}));
		sendSocketCommand.mockResolvedValue({ data: { attributes: serverResult, statPointsUsed: 1 } });

		view.inc(idx.str);
		await view.save();

		expect(sendSocketCommand).toHaveBeenCalledTimes(1);
		expect(sendSocketCommand).toHaveBeenCalledWith('UpdatePlayerStats', [
			{ attributeId: EAttribute.Strength, amount: 1 }
		]);
		// Applied to the player manager so battles and other screens see it.
		expect(mockPlayerManager.attributes).toEqual(serverResult);
		expect(mockPlayerManager.statPointsUsed).toBe(1);
		expect(view.dirty).toBe(false);
		expect(view.committed[idx.str]).toBe(6);
		expect(view.remaining).toBe(9);
		expect(view.saved).toBe(true);
		expect(toastError).not.toHaveBeenCalled();
	});

	it('adopts the server statPointsUsed absolutely, not as a relative increment (#1548)', async () => {
		// The server is authoritative: if its post-command total disagrees with the
		// locally-derivable `+= netSpent` (e.g. a concurrent victory reconcile), its
		// value must win outright.
		sendSocketCommand.mockResolvedValue({ data: { attributes: allFives(), statPointsUsed: 6 } });

		view.inc(idx.str);
		await view.save();

		expect(mockPlayerManager.statPointsUsed).toBe(6);
		expect(view.savedAvailable).toBe(4);
	});

	it('warns and keeps the changes when the request fails without data', async () => {
		sendSocketCommand.mockResolvedValue({ error: 'Socket closed.', data: undefined });
		view.inc(idx.str);
		await view.save();

		expect(toastError).toHaveBeenCalledTimes(1);
		expect(view.dirty).toBe(true);
		expect(view.values[idx.str]).toBe(6);
		expect(mockPlayerManager.statPointsUsed).toBe(0);
	});

	it('reconciles onto the returned unchanged state when the server rejects the save', async () => {
		// The rejection response still carries the authoritative (unchanged) state, so the
		// reconcile applies while the draft stays dirty for retry.
		sendSocketCommand.mockResolvedValue({
			error: 'Unable to update player stats.',
			data: { attributes: allFives(), statPointsUsed: 2 }
		});
		view.inc(idx.str);
		await view.save();

		expect(toastError).toHaveBeenCalledTimes(1);
		expect(mockPlayerManager.attributes).toEqual(allFives());
		expect(mockPlayerManager.statPointsUsed).toBe(2);
		expect(view.dirty).toBe(true);
		expect(view.values[idx.str]).toBe(6);
		expect(view.committed[idx.str]).toBe(5); // baseline unadvanced
		expect(view.saved).toBe(false);
	});

	it('preserves draft edits made while the save is in flight (#1506)', async () => {
		const serverResult: IBattlerAttribute[] = CORE_ATTRIBUTES.map((id) => ({
			attributeId: id,
			amount: id === EAttribute.Strength ? 6 : 5
		}));
		let resolveSend: (value: unknown) => void = () => {};
		sendSocketCommand.mockReturnValue(new Promise((resolve) => (resolveSend = resolve)));

		view.inc(idx.str);
		const saving = view.save();
		// Allocate another point while the request is in flight — it was not sent.
		view.inc(idx.end);
		resolveSend({ data: { attributes: serverResult, statPointsUsed: 1 } });
		await saving;

		// The baseline advanced by exactly the sent delta; the mid-flight edit is still
		// pending rather than silently wiped, and is exactly what a retry would send.
		expect(view.committed[idx.str]).toBe(6);
		expect(view.committed[idx.end]).toBe(5);
		expect(view.values[idx.end]).toBe(6);
		expect(view.dirty).toBe(true);
		expect(view.changedUpdates).toEqual([{ attributeId: EAttribute.Endurance, amount: 1 }]);
		// Pool 9 after the spend, minus the 1 still-pending point.
		expect(view.remaining).toBe(8);
	});

	it('does nothing when there are no changes', async () => {
		await view.save();
		expect(sendSocketCommand).not.toHaveBeenCalled();
		expect(toastError).not.toHaveBeenCalled();
	});
});

describe('AttributesView live stat-point pool', () => {
	it('reflects stat points granted by a background level-up without a remount (#1506)', () => {
		expect(view.savedAvailable).toBe(10);
		expect(view.remaining).toBe(10);

		// A background victory levels the player up while the screen is open.
		mockPlayerManager.statPointsGained += 5;

		expect(view.savedAvailable).toBe(15);
		expect(view.remaining).toBe(15);
		expect(view.budget).toBe(15);
	});
});
