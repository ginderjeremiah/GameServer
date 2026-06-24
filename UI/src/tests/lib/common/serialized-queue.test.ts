import { describe, it, expect } from 'vitest';
import { SerializedQueue } from '$lib/common';

// SerializedQueue runs each enqueued operation only after the previous one has fully settled, returning
// a promise for each operation's own result and keeping its internal tail always-fulfilled so one
// failure can't break the chain.

/** A manually-resolved promise plus its resolve/reject handles, for driving settlement order in tests. */
function deferred<T>() {
	let resolve!: (value: T) => void;
	let reject!: (reason?: unknown) => void;
	const promise = new Promise<T>((res, rej) => {
		resolve = res;
		reject = rej;
	});
	return { promise, resolve, reject };
}

describe('SerializedQueue', () => {
	it('returns each operation result to its own caller', async () => {
		const queue = new SerializedQueue();
		const a = queue.run(async () => 'a');
		const b = queue.run(async () => 'b');
		expect(await a).toBe('a');
		expect(await b).toBe('b');
	});

	it('does not start an operation until the previous one has settled', async () => {
		const queue = new SerializedQueue();
		const order: string[] = [];
		const first = deferred<void>();

		const a = queue.run(async () => {
			order.push('a-start');
			await first.promise;
			order.push('a-end');
		});
		const b = queue.run(async () => {
			order.push('b-start');
		});

		// `b` must wait for `a` to settle, even though it was enqueued immediately.
		await Promise.resolve();
		expect(order).toEqual(['a-start']);

		first.resolve();
		await Promise.all([a, b]);
		expect(order).toEqual(['a-start', 'a-end', 'b-start']);
	});

	it('rejects the failing operation to its caller but still runs later operations', async () => {
		const queue = new SerializedQueue();
		const failing = queue.run(async () => {
			throw new Error('boom');
		});
		const next = queue.run(async () => 'after');

		await expect(failing).rejects.toThrow('boom');
		expect(await next).toBe('after');
	});

	it('runs operations in enqueue order', async () => {
		const queue = new SerializedQueue();
		const order: number[] = [];
		const runs = [0, 1, 2, 3].map((n) =>
			queue.run(async () => {
				order.push(n);
			})
		);
		await Promise.all(runs);
		expect(order).toEqual([0, 1, 2, 3]);
	});
});
