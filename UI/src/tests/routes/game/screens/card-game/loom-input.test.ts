// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from 'vitest';
import { runLoomLoop, bindLoomInput } from '$routes/game/screens/card-game/loom-input';
import type { CardGameView } from '$routes/game/screens/card-game/card-game-view.svelte';

/** A minimal stand-in for the view: only the members the input drivers touch. */
function fakeView(over = false) {
	return {
		game: { over, advance: vi.fn() },
		setReflex: vi.fn(),
		castSlot: vi.fn()
	} as unknown as CardGameView & { game: { over: boolean; advance: ReturnType<typeof vi.fn> } };
}

afterEach(() => {
	document.body.innerHTML = '';
	vi.restoreAllMocks();
});

describe('runLoomLoop', () => {
	it('advances the game by the inter-frame delta each frame and stops on teardown', () => {
		// An object slot (not a local) so TS keeps `frame` as the callback type across the rAF closure.
		const scheduled: { frame?: FrameRequestCallback } = {};
		let nextId = 1;
		const cancelled: number[] = [];
		vi.stubGlobal('requestAnimationFrame', (fn: FrameRequestCallback) => {
			scheduled.frame = fn;
			return nextId++;
		});
		vi.stubGlobal('cancelAnimationFrame', (id: number) => cancelled.push(id));

		const view = fakeView();
		const stop = runLoomLoop(view);

		// First frame seeds `last`, so dt is 0; the second advances by the elapsed seconds.
		scheduled.frame?.(0);
		scheduled.frame?.(1000);
		expect(view.game.advance).toHaveBeenNthCalledWith(1, 0);
		expect(view.game.advance).toHaveBeenNthCalledWith(2, 1);

		stop();
		// The most recently scheduled frame id is cancelled.
		expect(cancelled).toContain(nextId - 1);
	});
});

describe('bindLoomInput', () => {
	const keydown = (init: KeyboardEventInit) => window.dispatchEvent(new KeyboardEvent('keydown', init));
	const keyup = (init: KeyboardEventInit) => window.dispatchEvent(new KeyboardEvent('keyup', init));

	it('holds Reflex while Space is down and releases it on key up', () => {
		const view = fakeView();
		const unbind = bindLoomInput(view);

		keydown({ code: 'Space' });
		expect(view.setReflex).toHaveBeenLastCalledWith(true);

		keyup({ code: 'Space' });
		expect(view.setReflex).toHaveBeenLastCalledWith(false);

		unbind();
	});

	it('ignores auto-repeat so a held Space only engages Reflex once', () => {
		const view = fakeView();
		const unbind = bindLoomInput(view);

		keydown({ code: 'Space', repeat: true });
		expect(view.setReflex).not.toHaveBeenCalled();

		unbind();
	});

	it('quick-casts the hand slot for number keys 1–7', () => {
		const view = fakeView();
		const unbind = bindLoomInput(view);

		keydown({ key: '3' });
		expect(view.castSlot).toHaveBeenCalledWith(2);

		unbind();
	});

	it('yields hotkeys to a focused text input so sliders/fields keep working', () => {
		const input = document.createElement('input');
		document.body.appendChild(input);
		input.focus();

		const view = fakeView();
		const unbind = bindLoomInput(view);

		keydown({ key: '3' });
		expect(view.castSlot).not.toHaveBeenCalled();

		unbind();
	});

	it('does not cast once the duel is over', () => {
		const view = fakeView(true);
		const unbind = bindLoomInput(view);

		keydown({ key: '3' });
		expect(view.castSlot).not.toHaveBeenCalled();

		unbind();
	});

	it('ends a held Reflex on a pointer release or cancel anywhere', () => {
		const view = fakeView();
		const unbind = bindLoomInput(view);

		window.dispatchEvent(new Event('pointerup'));
		expect(view.setReflex).toHaveBeenLastCalledWith(false);

		window.dispatchEvent(new Event('pointercancel'));
		expect(view.setReflex).toHaveBeenLastCalledWith(false);

		unbind();
	});

	it('removes every listener on teardown', () => {
		const view = fakeView();
		bindLoomInput(view)();

		keydown({ code: 'Space' });
		window.dispatchEvent(new Event('pointerup'));
		expect(view.setReflex).not.toHaveBeenCalled();
	});
});
