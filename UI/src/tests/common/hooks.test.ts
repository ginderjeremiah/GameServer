import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('svelte', () => ({
	onDestroy: vi.fn((fn: () => void) => fn)
}));

import { createHook } from '../../lib/common/hooks';
import { onDestroy } from 'svelte';

describe('createHook', () => {
	beforeEach(() => {
		vi.mocked(onDestroy).mockClear();
	});

	it('delivers notifications to all subscribers', () => {
		const hook = createHook<[number]>();
		const cb1 = vi.fn();
		const cb2 = vi.fn();

		hook.onNotified(cb1);
		hook.onNotified(cb2);
		hook.notify(42);

		expect(cb1).toHaveBeenCalledWith(42, expect.any(Function));
		expect(cb2).toHaveBeenCalledWith(42, expect.any(Function));
	});

	it('passes multiple arguments to subscribers', () => {
		const hook = createHook<[string, number]>();
		const cb = vi.fn();

		hook.onNotified(cb);
		hook.notify('hello', 5);

		expect(cb).toHaveBeenCalledWith('hello', 5, expect.any(Function));
	});

	it('unhook removes the subscriber', () => {
		const hook = createHook<[number]>();
		const cb = vi.fn();

		const unhook = hook.onNotified(cb);
		hook.notify(1);
		expect(cb).toHaveBeenCalledTimes(1);

		unhook();
		hook.notify(2);
		expect(cb).toHaveBeenCalledTimes(1);
	});

	it('unhook outside dispatch splices only the targeted subscriber', () => {
		const hook = createHook<[number]>();
		const cb1 = vi.fn();
		const cb2 = vi.fn();
		const cb3 = vi.fn();

		hook.onNotified(cb1);
		const unhook2 = hook.onNotified(cb2);
		hook.onNotified(cb3);

		// Removing a middle subscriber while no dispatch is in progress takes the
		// immediate-splice path; its siblings must keep firing afterwards.
		unhook2();
		hook.notify(1);

		expect(cb1).toHaveBeenCalledWith(1, expect.any(Function));
		expect(cb2).not.toHaveBeenCalled();
		expect(cb3).toHaveBeenCalledWith(1, expect.any(Function));
	});

	it('unhook is idempotent', () => {
		const hook = createHook<[number]>();
		const cb = vi.fn();

		const unhook = hook.onNotified(cb);
		unhook();
		unhook();

		hook.notify(1);
		expect(cb).not.toHaveBeenCalled();
	});

	it('does not wire onDestroy by default (safe outside component init)', () => {
		const hook = createHook<[number]>();
		const cb = vi.fn();

		// Default subscribe must not touch the framework lifecycle, so a module-level / async
		// caller can subscribe without onDestroy throwing — and still gets a working unsubscribe.
		const unhook = hook.onNotified(cb);
		expect(onDestroy).not.toHaveBeenCalled();

		hook.notify(1);
		expect(cb).toHaveBeenCalledTimes(1);

		unhook();
		hook.notify(2);
		expect(cb).toHaveBeenCalledTimes(1);
	});

	it('wires onDestroy cleanup when opted in (component context)', () => {
		const hook = createHook<[]>();
		hook.onNotified(vi.fn(), true);

		expect(onDestroy).toHaveBeenCalledTimes(1);
		expect(onDestroy).toHaveBeenCalledWith(expect.any(Function));
	});

	it('works with zero-arg hooks', () => {
		const hook = createHook();
		const cb = vi.fn();

		hook.onNotified(cb);
		hook.notify();

		expect(cb).toHaveBeenCalledWith(expect.any(Function));
	});

	it('still fires remaining subscribers when one unhooks itself during dispatch', () => {
		const hook = createHook<[number]>();
		const cb1 = vi.fn();
		const cb2 = vi.fn();

		// cb1 unhooks itself from inside its own callback, shifting cb2 into
		// the just-visited slot — cb2 must not be skipped for this dispatch.
		hook.onNotified((value, unhook) => {
			cb1(value);
			unhook();
		});
		hook.onNotified(cb2);

		hook.notify(1);

		expect(cb1).toHaveBeenCalledTimes(1);
		expect(cb2).toHaveBeenCalledTimes(1);
		expect(cb2).toHaveBeenCalledWith(1, expect.any(Function));

		// cb1 is gone for the next dispatch; cb2 keeps firing.
		hook.notify(2);
		expect(cb1).toHaveBeenCalledTimes(1);
		expect(cb2).toHaveBeenCalledTimes(2);
	});

	it('still fires remaining subscribers when one unhooks another during dispatch', () => {
		const hook = createHook<[number]>();
		const cb1 = vi.fn();
		const cb3 = vi.fn();

		hook.onNotified((value) => {
			cb1(value);
			// Unhook the next subscriber before it has been visited.
			unhookSecond();
		});
		const unhookSecond = hook.onNotified(vi.fn());
		hook.onNotified(cb3);

		hook.notify(7);

		// Removing a not-yet-visited subscriber mid-dispatch must not knock the
		// snapshot off and skip the subscriber after it.
		expect(cb1).toHaveBeenCalledTimes(1);
		expect(cb3).toHaveBeenCalledWith(7, expect.any(Function));
	});

	it('subscriber added after notify does not receive past notifications', () => {
		const hook = createHook<[number]>();

		hook.notify(1);

		const cb = vi.fn();
		hook.onNotified(cb);

		expect(cb).not.toHaveBeenCalled();
	});

	it('subscriber added during dispatch is deferred to the next notify', () => {
		const hook = createHook<[number]>();
		const added = vi.fn();

		// A subscriber that registers another subscriber while handling the event.
		// The new subscriber must not receive the in-progress notification (it
		// subscribed after the event fired) but must receive subsequent ones.
		hook.onNotified((value) => {
			if (value === 1) {
				hook.onNotified(added);
			}
		});

		hook.notify(1);
		expect(added).not.toHaveBeenCalled();

		hook.notify(2);
		expect(added).toHaveBeenCalledTimes(1);
		expect(added).toHaveBeenCalledWith(2, expect.any(Function));
	});

	it('compaction after a deferred add keeps the new subscriber and drops the tombstoned one', () => {
		const hook = createHook<[number]>();
		const cbA = vi.fn();
		const cbB = vi.fn();
		const cbC = vi.fn();

		// One callback both tombstones itself and appends a new subscriber in the
		// same dispatch — the post-dispatch compaction must splice the tombstone yet
		// preserve the appended subscriber so it fires on the following notify.
		hook.onNotified((value, unhook) => {
			cbA(value);
			if (value === 1) {
				unhook();
				hook.onNotified(cbC);
			}
		});
		hook.onNotified(cbB);

		hook.notify(1);
		expect(cbA).toHaveBeenCalledTimes(1);
		expect(cbB).toHaveBeenCalledTimes(1);
		expect(cbC).not.toHaveBeenCalled();

		hook.notify(2);
		expect(cbA).toHaveBeenCalledTimes(1);
		expect(cbB).toHaveBeenCalledTimes(2);
		expect(cbC).toHaveBeenCalledTimes(1);
		expect(cbC).toHaveBeenCalledWith(2, expect.any(Function));
	});
});
