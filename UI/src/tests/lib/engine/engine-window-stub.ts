import { vi } from 'vitest';

/* Shared `window` stub for the engine suites (#2489), replacing three hand-rolled `vi.stubGlobal`
   blocks that disagreed on which members they defined and on how rAF handles were numbered.

   Co-located with its consumers rather than placed in `tests/fixtures/`, which `docs/frontend.md` →
   Testing Guidelines reserves for `make<Contract>(overrides)` builders of a typed contract. This is an
   environment seam, not a contract, and every caller lives in this folder — same shape as
   `progression-test-utils.ts`.

   It defines only the four members the engines actually reach for: `createTickSource` falls back to
   `window.setInterval`/`clearInterval` when `Worker` is absent (jsdom), and `RenderEngine` drives its
   loop off `window.requestAnimationFrame`/`cancelAnimationFrame`. A fuller `window` shape would invite
   a suite to lean on a member that isn't one of those seams; widening it is now one edit here rather
   than a fourth copy. */

export interface EngineWindowStub {
	/** rAF callbacks scheduled and not yet run, oldest first — `shift()` one and call it to drive a frame. */
	readonly rafCallbacks: (() => void)[];
	/** Handles passed to `cancelAnimationFrame`, in call order. */
	readonly cancelledHandles: number[];
	/** Clears both recordings and restarts handle numbering. Call from a suite's `beforeEach`. */
	reset(): void;
}

/**
 * Stubs the global `window` with the timer and animation-frame members the engines use, recording
 * what they were handed. Call once at module scope, as the raw `vi.stubGlobal` blocks were: the
 * engines read `window` lazily (at start/stop, never at import), so the stub only has to be installed
 * before the first test runs, not before the engine modules are imported.
 */
export const stubEngineWindow = (): EngineWindowStub => {
	const rafCallbacks: (() => void)[] = [];
	const cancelledHandles: number[] = [];
	// Monotonic, not `rafCallbacks.length`: the suites `shift()` callbacks off the front, which would
	// hand out the same handle twice and break the cancellation assertions.
	let rafHandleCounter = 0;

	vi.stubGlobal('window', {
		requestAnimationFrame: vi.fn((cb: () => void) => {
			rafCallbacks.push(cb);
			return ++rafHandleCounter;
		}),
		cancelAnimationFrame: vi.fn((handle: number) => {
			cancelledHandles.push(handle);
		}),
		setInterval: (...args: Parameters<typeof setInterval>) => globalThis.setInterval(...args),
		clearInterval: (...args: Parameters<typeof clearInterval>) => globalThis.clearInterval(...args)
	});

	return {
		rafCallbacks,
		cancelledHandles,
		reset: () => {
			rafCallbacks.length = 0;
			cancelledHandles.length = 0;
			rafHandleCounter = 0;
		}
	};
};
