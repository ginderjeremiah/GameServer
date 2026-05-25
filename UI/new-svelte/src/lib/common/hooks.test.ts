import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('svelte', () => ({
	onDestroy: vi.fn((fn: () => void) => fn)
}));

import { createHook } from './hooks';
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

	it('subscriber added after notify does not receive past notifications', () => {
		const hook = createHook<[number]>();

		hook.notify(1);

		const cb = vi.fn();
		hook.onNotified(cb);

		expect(cb).not.toHaveBeenCalled();
	});
});
