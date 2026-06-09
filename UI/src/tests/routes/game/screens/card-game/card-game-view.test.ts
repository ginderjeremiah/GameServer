import { describe, it, expect, beforeEach } from 'vitest';
import { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';
import { NOW_FRACTION, PX_PER_TICK } from '$lib/card-game';

/* The view-model wraps the (already unit-tested) LoomGame and adds the screen's
   UI math: NOW-line geometry, pointer→tick conversion, the drag-threshold gate,
   and placement-preview derivation. These tests keep the simulation static
   (no `advance`) so both board lanes stay empty and placement is deterministic —
   the goal is the view-model's translation of pointer gestures into game calls,
   not LoomGame's casting/scheduling, which loom-game.test.ts already covers. */

let view: CardGameView;

beforeEach(() => {
	view = new CardGameView();
});

describe('CardGameView — board geometry', () => {
	it('places the NOW line at NOW_FRACTION of the board width', () => {
		view.boardWidth = 200;
		expect(view.nowX).toBeCloseTo(200 * NOW_FRACTION, 5);
	});

	it('keeps the NOW line at zero before the board has been measured', () => {
		expect(view.boardWidth).toBe(0);
		expect(view.nowX).toBe(0);
	});
});

describe('CardGameView — pointer→tick conversion', () => {
	beforeEach(() => {
		view.boardWidth = 200; // nowX = 60
		view.boardLeft = 0;
	});

	it('converts a client x to the board tick under it, rounding to the nearest tick', () => {
		// (clientX - boardLeft - nowX) / PX_PER_TICK + playTick, with playTick 0.
		expect(view.tickAtClientX(60 + 5 * PX_PER_TICK)).toBe(5);
		expect(view.tickAtClientX(60 + PX_PER_TICK / 2 + 1)).toBe(1); // rounds up past the half-tick
	});

	it('offsets the conversion by the board left edge', () => {
		view.boardLeft = 20;
		expect(view.tickAtClientX(20 + 60 + 4 * PX_PER_TICK)).toBe(4);
	});

	it('adds the current scroll position (playTick) so ticks stay absolute', () => {
		view.game.playTick = 3;
		expect(view.tickAtClientX(60 + 5 * PX_PER_TICK)).toBe(8);
	});
});

describe('CardGameView — drag threshold gate', () => {
	it('records a drag on begin but does not arm it until the pointer moves', () => {
		view.beginDrag(0, 'slash', 100, 100);
		expect(view.drag).toMatchObject({ index: 0, key: 'slash', pointerX: 100, started: false });
	});

	it('keeps the drag un-started for a jitter within the threshold while still tracking the pointer', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.moveDrag(104, 103); // hypot(4,3) = 5, within the 6px threshold
		expect(view.drag?.started).toBe(false);
		expect(view.drag?.pointerX).toBe(104);
	});

	it('does not arm exactly at the threshold (strictly greater than 6px)', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.moveDrag(106, 100); // exactly 6px
		expect(view.drag?.started).toBe(false);
	});

	it('arms the drag once the pointer travels past the threshold', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.moveDrag(110, 100); // 10px > 6px
		expect(view.drag?.started).toBe(true);
	});

	it('ignores moves when there is no active drag', () => {
		view.moveDrag(200, 200);
		expect(view.drag).toBeNull();
	});

	it('refuses to begin a drag once the duel is over', () => {
		view.game.over = true;
		view.beginDrag(0, 'slash', 100, 100);
		expect(view.drag).toBeNull();
	});
});

describe('CardGameView — placement preview', () => {
	beforeEach(() => {
		view.boardWidth = 200; // nowX = 60
		view.boardLeft = 0;
	});

	it('is null when nothing is hovered or dragged', () => {
		expect(view.preview).toBeNull();
	});

	it('derives the hover hint from the lane tail and flags it as a hint', () => {
		view.setHover('slash'); // attack lane, windup 3, empty lane → starts at tick 0
		expect(view.preview).toEqual({ key: 'slash', kind: 'attack', start: 0, dur: 3, hint: true });
	});

	it('aims an armed drag to the tick under the pointer (not a hint)', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.moveDrag(60 + 5 * PX_PER_TICK, 100); // arms the drag and aims at tick 5
		expect(view.preview).toEqual({ key: 'slash', kind: 'attack', start: 5, dur: 3, hint: false });
	});

	it('shows the hover hint while a drag is recorded but not yet armed', () => {
		view.setHover('guard');
		view.beginDrag(0, 'slash', 100, 100); // recorded, started === false
		expect(view.preview).toMatchObject({ key: 'guard', hint: true });
	});

	it('is null once the duel is over, even while hovering', () => {
		view.setHover('slash');
		view.game.over = true;
		expect(view.preview).toBeNull();
	});
});

describe('CardGameView — hover hint', () => {
	it('sets and clears the hovered card', () => {
		view.setHover('guard');
		expect(view.hoverKey).toBe('guard');
		view.clearHover('guard');
		expect(view.hoverKey).toBeNull();
	});

	it('only clears the hover when the released card matches the hovered one', () => {
		view.setHover('guard');
		view.clearHover('slash'); // a different card left → no change
		expect(view.hoverKey).toBe('guard');
	});

	it('ignores hover once the duel is over', () => {
		view.game.over = true;
		view.setHover('guard');
		expect(view.hoverKey).toBeNull();
	});
});

describe('CardGameView — drag completion', () => {
	beforeEach(() => {
		// A deterministic single-card hand so the cast is observable on the board.
		view.game.hand = [{ id: 1, key: 'slash' }];
	});

	it('quick-casts to the lane tail on a click (no movement)', () => {
		view.beginDrag(0, 'slash', 100, 100); // never armed
		view.completeDrag(false, 0);
		expect(view.game.hand).toHaveLength(0);
		expect(view.game.discard).toEqual(['slash']);
		expect(view.game.ents.filter((e) => e.type === 'attack')).toHaveLength(1);
		expect(view.drag).toBeNull();
	});

	it('aims the cast to the released tick when dropped over the board', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.moveDrag(110, 100); // arm the drag
		view.completeDrag(true, 12);
		const strike = view.game.ents.find((e) => e.type === 'attack');
		expect([strike?.start, strike?.end]).toEqual([12, 15]); // slash windup 3
	});

	it('cancels (no cast) when an armed drag is released off the board', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.moveDrag(110, 100); // armed
		view.completeDrag(false, 12);
		expect(view.game.hand).toHaveLength(1); // still in hand
		expect(view.game.ents).toHaveLength(0);
		expect(view.drag).toBeNull();
	});

	it('clears the hover hint after completing a drag', () => {
		view.setHover('slash');
		view.beginDrag(0, 'slash', 100, 100);
		view.completeDrag(false, 0);
		expect(view.hoverKey).toBeNull();
	});

	it('is a no-op when there is no active drag', () => {
		view.completeDrag(true, 5);
		expect(view.game.hand).toHaveLength(1);
		expect(view.game.ents).toHaveLength(0);
	});

	it('ignores completion once the duel is over but still clears the in-flight drag', () => {
		view.beginDrag(0, 'slash', 100, 100);
		view.game.over = true;
		view.completeDrag(false, 0);
		expect(view.game.hand).toHaveLength(1); // nothing cast
		expect(view.drag).toBeNull();
	});
});

describe('CardGameView — input passthroughs', () => {
	it('delegates castSlot to the game', () => {
		view.game.hand = [{ id: 1, key: 'slash' }];
		view.castSlot(0);
		expect(view.game.hand).toHaveLength(0);
		expect(view.game.discard).toEqual(['slash']);
	});

	it('delegates setReflex to the game', () => {
		view.setReflex(true);
		expect(view.game.slow).toBe(true);
	});

	it('delegates setStat to the game', () => {
		view.setStat('agi', 42);
		expect(view.game.agi).toBe(42);
	});

	it('delegates reset to the game', () => {
		view.game.playTick = 25;
		view.reset();
		expect(view.game.playTick).toBe(0);
		expect(view.game.hand).toHaveLength(4);
	});
});
