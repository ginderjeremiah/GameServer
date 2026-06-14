// @vitest-environment jsdom
import { describe, it, expect, afterEach, beforeAll } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import CardGame from '../../../../../routes/game/screens/card-game/CardGame.svelte';

// jsdom lacks ResizeObserver, which Svelte 5 uses to back the board's
// `bind:clientWidth`. Stub it so the component can mount under test (the real
// browser provides it).
beforeAll(() => {
	globalThis.ResizeObserver ??= class {
		observe() {}
		unobserve() {}
		disconnect() {}
	} as unknown as typeof ResizeObserver;
});

afterEach(cleanup);

// A mount smoke-test: the screen is auth-gated in the running app, so this is the
// cheapest way to prove the component (statify-backed engine + rAF loop + onMount
// wiring) renders without a runtime error and shows its core furniture. The
// detailed mechanics are covered by the engine unit tests.
describe('CardGame screen', () => {
	it('mounts and renders the duel furniture', () => {
		const { container, getByText } = render(CardGame);

		expect(container.querySelector('[data-testid="card-game-screen"]')).not.toBeNull();
		// both combatants
		expect(getByText('The Warden')).toBeTruthy();
		expect(getByText('You')).toBeTruthy();
		// the board and a full opening hand (4 cards, no ghosts at the default cap)
		expect(container.querySelector('.board')).not.toBeNull();
		expect(container.querySelectorAll('.card')).toHaveLength(4);
		// the sandbox attribute strip is present
		expect(getByText('sandbox')).toBeTruthy();
	});

	// The Reflex button is press-and-hold: the global release listener must end the hold even when
	// the pointer is released off the button, on touch as on mouse (the old code listened to mouseup).
	it('ends a held Reflex when the pointer is released anywhere', async () => {
		const { container } = render(CardGame);
		const reflex = container.querySelector('.btn.reflex') as HTMLElement;

		await fireEvent.pointerDown(reflex);
		expect(reflex.classList.contains('on')).toBe(true);

		await fireEvent.pointerUp(window);
		expect(reflex.classList.contains('on')).toBe(false);
	});

	it('ends a held Reflex when the pointer gesture is cancelled', async () => {
		const { container } = render(CardGame);
		const reflex = container.querySelector('.btn.reflex') as HTMLElement;

		await fireEvent.pointerDown(reflex);
		expect(reflex.classList.contains('on')).toBe(true);

		await fireEvent.pointerCancel(window);
		expect(reflex.classList.contains('on')).toBe(false);
	});
});
