/* loom-input.ts — the DOM-side drivers for the Card-Boss Duel screen, kept out of the component so the
   real-time render loop and the global input bindings each read as one self-contained, testable unit
   (and out of the DOM-free `$lib/card-game` engine, which never touches `window`/`requestAnimationFrame`).

   Each binder owns its own setup + teardown and returns the teardown; the screen's `onMount` composes them. */

import type { CardGameView } from './card-game-view.svelte';

/** Drives the real-time simulation from a requestAnimationFrame loop — the present advances on its own.
 *  Returns a stop function that cancels the pending frame. */
export function runLoomLoop(view: CardGameView): () => void {
	let last: number | null = null;
	let raf = 0;
	const loop = (ts: number) => {
		if (last === null) {
			last = ts;
		}
		const dt = (ts - last) / 1000;
		last = ts;
		view.game.advance(dt);
		raf = requestAnimationFrame(loop);
	};
	raf = requestAnimationFrame(loop);
	return () => cancelAnimationFrame(raf);
}

/** Binds the screen's global input: 1–7 quick-cast a hand slot, hold Space for Reflex slow-time, and a
 *  pointer release/cancel anywhere ends a held Reflex (the button is press-and-hold, so the release can
 *  land off the button). Returns a teardown that removes every listener. */
export function bindLoomInput(view: CardGameView): () => void {
	// Hotkeys yield to focused sandbox sliders and buttons so they keep working (a focused button's own
	// Space/Enter handling — e.g. the Reflex button's press-and-hold — takes precedence over the global one).
	const onKeyDown = (e: KeyboardEvent) => {
		const ae = document.activeElement;
		if (ae && (ae.tagName === 'INPUT' || ae.tagName === 'TEXTAREA' || ae.tagName === 'BUTTON')) {
			return;
		}
		if (e.code === 'Space') {
			e.preventDefault();
			if (!e.repeat) {
				view.setReflex(true);
			}
			return;
		}
		if (view.game.over) {
			return;
		}
		if (e.key >= '1' && e.key <= '7') {
			view.castSlot(+e.key - 1);
		}
	};
	const onKeyUp = (e: KeyboardEvent) => {
		if (e.code === 'Space') {
			view.setReflex(false);
		}
	};
	const endReflex = () => view.setReflex(false);

	window.addEventListener('keydown', onKeyDown);
	window.addEventListener('keyup', onKeyUp);
	window.addEventListener('pointerup', endReflex);
	window.addEventListener('pointercancel', endReflex);

	return () => {
		window.removeEventListener('keydown', onKeyDown);
		window.removeEventListener('keyup', onKeyUp);
		window.removeEventListener('pointerup', endReflex);
		window.removeEventListener('pointercancel', endReflex);
	};
}
