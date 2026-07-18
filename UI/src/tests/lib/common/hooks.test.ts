import { describe, it, expect, vi } from 'vitest';

vi.mock('svelte', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	onDestroy: vi.fn()
}));

import { createHook } from '$lib/common';

describe('createHook', () => {
	it('notifies subscribers in subscription order with the passed data', () => {
		const hook = createHook<[number]>();
		const calls: number[] = [];
		hook.onNotified((n) => calls.push(n));
		hook.onNotified((n) => calls.push(n * 10));

		hook.notify(1);

		expect(calls).toEqual([1, 10]);
	});

	it('a subscriber that unhooks itself is skipped for the rest of the current dispatch', () => {
		const hook = createHook();
		const calls: string[] = [];
		hook.onNotified((unhook) => {
			calls.push('a');
			unhook();
		});
		hook.onNotified(() => calls.push('b'));

		hook.notify();
		expect(calls).toEqual(['a', 'b']);

		calls.length = 0;
		hook.notify();
		expect(calls).toEqual(['b']); // the self-unhooked subscriber no longer fires
	});

	it('a subscriber added during dispatch does not fire until the next notify', () => {
		const hook = createHook();
		const calls: string[] = [];
		hook.onNotified(() => {
			calls.push('first');
			hook.onNotified(() => calls.push('added-mid-dispatch'));
		});

		hook.notify();
		expect(calls).toEqual(['first']);

		hook.notify();
		expect(calls).toEqual(['first', 'first', 'added-mid-dispatch']);
	});

	it('unhooking a subscriber ahead of the current walk index during dispatch excludes it from this pass', () => {
		const hook = createHook();
		const calls: string[] = [];
		let unhookB: () => void = () => {};
		hook.onNotified(() => {
			calls.push('a');
			unhookB();
		});
		hook.onNotified(() => calls.push('b'));
		// Capture b's own unhook by re-subscribing and grabbing the returned function.
		unhookB = hook.onNotified(() => calls.push('c'));

		hook.notify();
		expect(calls).toEqual(['a', 'b']); // c was tombstoned before its turn in this pass
	});

	it('a callback that synchronously re-notifies the same hook does not corrupt the outer dispatch (regression)', () => {
		// Without a reentrancy-safe depth counter, the inner notify's epilogue would reset the
		// dispatch flag to false while the outer walk is still mid-loop, letting a subsequent
		// unhook splice immediately (shifting indices under the outer loop) instead of tombstoning.
		const hook = createHook();
		const calls: string[] = [];
		let reentered = false;

		hook.onNotified((unhook) => {
			calls.push('outer-a');
			if (!reentered) {
				reentered = true;
				hook.notify(); // synchronous same-hook re-entry
			}
			unhook(); // unhooks outer-a itself, mid-outer-walk
		});
		hook.onNotified(() => calls.push('outer-b'));

		hook.notify();

		// outer-a fires once for the outer call and once for the nested call; outer-b fires
		// once for each pass that reaches it (the nested notify's pass, then the outer's own).
		expect(calls).toEqual(['outer-a', 'outer-a', 'outer-b', 'outer-b']);

		calls.length = 0;
		hook.notify();
		expect(calls).toEqual(['outer-b']); // outer-a's self-unhook took effect cleanly
	});
});
