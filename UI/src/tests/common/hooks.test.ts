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

	it('registers onDestroy cleanup by default', () => {
		const hook = createHook<[]>();
		hook.onNotified(vi.fn());

		expect(onDestroy).toHaveBeenCalledTimes(1);
		expect(onDestroy).toHaveBeenCalledWith(expect.any(Function));
	});

	it('skips onDestroy when cleanupOnDestroy is false', () => {
		const hook = createHook<[]>();
		hook.onNotified(vi.fn(), false);

		expect(onDestroy).not.toHaveBeenCalled();
	});

	it('nextNotification resolves on next notify', async () => {
		const hook = createHook<[string]>();

		const promise = hook.nextNotification();
		hook.notify('result');

		const value = await promise;
		expect(value).toEqual(['result']);
	});

	it('nextNotification only resolves once', async () => {
		const hook = createHook<[number]>();

		const promise = hook.nextNotification();
		hook.notify(1);
		hook.notify(2);

		const value = await promise;
		expect(value).toEqual([1]);
	});

	it('multiple nextNotification promises all resolve on same notify', async () => {
		const hook = createHook<[number]>();

		const p1 = hook.nextNotification();
		const p2 = hook.nextNotification();
		hook.notify(99);

		expect(await p1).toEqual([99]);
		expect(await p2).toEqual([99]);
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
});
