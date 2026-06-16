// @vitest-environment jsdom
import { describe, it, expect, afterEach, beforeAll, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';
import { PX_PER_TICK } from '$lib/card-game';
import Board from '$routes/game/screens/card-game/loom/Board.svelte';

// jsdom lacks ResizeObserver, which Svelte 5 uses to back the board's `bind:clientWidth`.
beforeAll(() => {
	globalThis.ResizeObserver ??= class {
		observe() {}
		unobserve() {}
		disconnect() {}
	} as unknown as typeof ResizeObserver;
});

afterEach(cleanup);

// A 600px board → nowX = 600 * NOW_FRACTION (0.3) = 180.
const BOARD_RECT = {
	left: 0,
	top: 0,
	right: 600,
	bottom: 184,
	width: 600,
	height: 184,
	x: 0,
	y: 0,
	toJSON: () => ({})
} as DOMRect;

const setup = () => {
	const view = new CardGameView();
	view.game.hand = [{ id: 1, key: 'slash' }];
	const { container } = render(Board, { props: { view } });
	const board = container.querySelector('.board') as HTMLElement;
	vi.spyOn(board, 'getBoundingClientRect').mockReturnValue(BOARD_RECT);
	// `bind:clientWidth` stays 0 under jsdom, so pin the geometry the handlers read.
	view.boardWidth = 600; // nowX = 180
	return { view, board };
};

describe('Board — pointer drag wiring', () => {
	it('tracks an in-flight drag on a window pointermove', () => {
		const { view } = setup();
		view.beginDrag(0, 'slash', 100, 100);
		fireEvent.pointerMove(window, { clientX: 240, clientY: 90 });
		expect(view.drag?.started).toBe(true); // moved well past the threshold
		expect(view.drag?.pointerX).toBe(240);
	});

	it('completes an aimed cast on a window pointerup released over the board', () => {
		const { view } = setup();
		view.beginDrag(0, 'slash', 100, 100);
		const clientX = 180 + 5 * PX_PER_TICK; // nowX + 5 ticks → tick 5 (= 300px, inside the board)
		fireEvent.pointerMove(window, { clientX, clientY: 90 }); // arm the drag
		fireEvent.pointerUp(window, { clientX, clientY: 90 });

		expect(view.game.hand).toHaveLength(0);
		const strike = view.game.ents.find((e) => e.type === 'attack');
		expect([strike?.start, strike?.end]).toEqual([5, 8]); // slash windup 3
	});

	it('quick-casts to the lane tail on a motion-free pointerup over the board', () => {
		const { view } = setup();
		view.beginDrag(0, 'slash', 100, 100); // never armed
		fireEvent.pointerUp(window, { clientX: 200, clientY: 90 });

		expect(view.game.hand).toHaveLength(0);
		const strike = view.game.ents.find((e) => e.type === 'attack');
		expect(strike?.start).toBe(0); // empty lane → casts to the tail at tick 0
	});

	it('aborts the drag without casting on a window pointercancel', () => {
		const { view } = setup();
		view.beginDrag(0, 'slash', 100, 100);
		fireEvent.pointerMove(window, { clientX: 240, clientY: 90 }); // arm it
		fireEvent.pointerCancel(window);

		expect(view.drag).toBeNull();
		expect(view.game.hand).toHaveLength(1); // still in hand — nothing cast
		expect(view.game.ents).toHaveLength(0);
	});

	it('no longer reacts to legacy mouse events, so the listeners are truly pointer-based', () => {
		const { view } = setup();
		view.beginDrag(0, 'slash', 100, 100);
		fireEvent.mouseMove(window, { clientX: 240, clientY: 90 });
		fireEvent.mouseUp(window, { clientX: 240, clientY: 90 });

		expect(view.drag).not.toBeNull(); // the mouse events did nothing
		expect(view.game.hand).toHaveLength(1);
	});

	it('ignores window pointer events when no drag is active', () => {
		const { view } = setup();
		expect(() => fireEvent.pointerMove(window, { clientX: 240, clientY: 90 })).not.toThrow();
		fireEvent.pointerUp(window, { clientX: 240, clientY: 90 });
		expect(view.drag).toBeNull();
		expect(view.game.hand).toHaveLength(1);
	});
});

describe('Board — flash rendering', () => {
	it('maps a semantic flash kind to its board y-position and themeable colour', () => {
		const view = new CardGameView();
		view.game.flashes = [
			{ id: 1, kind: 'strike', text: '-16', ttl: 0.6 },
			{ id: 2, kind: 'enemyHit', text: '-8', ttl: 0.6 }
		];
		const { container } = render(Board, { props: { view } });
		const flashes = Array.from(container.querySelectorAll('.flash')) as HTMLElement[];

		const strike = flashes.find((f) => f.textContent?.includes('-16'));
		expect(strike?.style.top).toBe('160px'); // player-strike row
		expect(strike?.style.color).toBe('var(--log-player)'); // player combat-log hue

		const enemyHit = flashes.find((f) => f.textContent?.includes('-8'));
		expect(enemyHit?.style.top).toBe('36px'); // enemy-impact row
		expect(enemyHit?.style.color).toBe('var(--enemy-accent)');
	});
});
