import { describe, it, expect, beforeEach } from 'vitest';
import { navigation, requiresRemount } from '$stores/navigation.svelte';

beforeEach(() => {
	// Reset the module-level intent between tests (clear the request, drain any pending payload).
	navigation.clear();
	navigation.consumePayload();
});

describe('navigation store', () => {
	it('requests a screen with no payload', () => {
		navigation.requestScreen('codex');
		expect(navigation.requestedScreen).toBe('codex');
		expect(navigation.hasPendingPayload).toBe(false);
		expect(navigation.consumePayload()).toBeUndefined();
	});

	it('carries a one-shot payload to the target and clears it on consume', () => {
		navigation.requestScreen('codex', { enemyId: 7 });
		expect(navigation.hasPendingPayload).toBe(true);
		expect(navigation.consumePayload<{ enemyId: number }>()).toEqual({ enemyId: 7 });
		// One-shot: a second read yields nothing and the pending flag is cleared.
		expect(navigation.hasPendingPayload).toBe(false);
		expect(navigation.consumePayload()).toBeUndefined();
	});

	it('clears only the requested screen, leaving the payload for the target to consume', () => {
		navigation.requestScreen('codex', { enemyId: 7 });
		navigation.clear();
		expect(navigation.requestedScreen).toBeNull();
		// The shell clears the request once it has switched, but the target still consumes its payload.
		expect(navigation.hasPendingPayload).toBe(true);
		expect(navigation.consumePayload<{ enemyId: number }>()).toEqual({ enemyId: 7 });
	});

	it('treats an explicit non-undefined payload (including null) as pending', () => {
		navigation.requestScreen('codex', null);
		expect(navigation.hasPendingPayload).toBe(true);
		expect(navigation.consumePayload()).toBeNull();
	});

	it('reset clears both the requested screen and any unconsumed payload', () => {
		navigation.requestScreen('codex', { enemyId: 7 });
		navigation.reset();
		expect(navigation.requestedScreen).toBeNull();
		expect(navigation.hasPendingPayload).toBe(false);
		expect(navigation.consumePayload()).toBeUndefined();
	});
});

describe('requiresRemount', () => {
	it('forces a remount only for a payload-bearing request to the already-active screen', () => {
		// Same screen + a queued payload → must remount so the target re-consumes the handoff.
		expect(requiresRemount('codex', 'codex', true)).toBe(true);
		// Same screen but no payload → leave the active screen untouched.
		expect(requiresRemount('codex', 'codex', false)).toBe(false);
		// Cross-screen → the dynamic component remounts on its own; no forced remount needed.
		expect(requiresRemount('codex', 'stats', true)).toBe(false);
		expect(requiresRemount('codex', 'stats', false)).toBe(false);
	});
});
