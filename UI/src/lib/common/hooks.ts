import { onDestroy } from 'svelte';
import { Action } from './types';

interface HookTracker<T extends unknown[]> {
	callback: Action<[...T, Action]>;
	unhook: Action;
	removed: boolean;
}

export const createHook = <T extends unknown[] = []>() => {
	let dispatching = false;
	let hasPendingRemovals = false;
	let promiseResolvers: Action<[T]>[] = [];
	const trackers: HookTracker<T>[] = [];

	const notify = (...data: T) => {
		for (const resolve of promiseResolvers) {
			resolve(data);
		}

		promiseResolvers = [];

		// Iterate by index without allocating a snapshot (this is one of the hottest
		// game-loop paths). A subscriber that unhooks mid-dispatch only tombstones
		// itself (`removed`) instead of splicing, so no sibling is shifted past the
		// cursor and skipped; the tombstones are compacted out once after dispatch.
		dispatching = true;
		for (let i = 0; i < trackers.length; i++) {
			const tracker = trackers[i];
			if (!tracker.removed) {
				tracker.callback(...data, tracker.unhook);
			}
		}
		dispatching = false;

		if (hasPendingRemovals) {
			for (let i = trackers.length - 1; i >= 0; i--) {
				if (trackers[i].removed) {
					trackers.splice(i, 1);
				}
			}
			hasPendingRemovals = false;
		}
	};

	const onNotified = (callback: Action<[...T, Action]>, cleanupOnDestroy: boolean = true) => {
		const unhook = () => {
			if (tracker.removed) {
				return;
			}
			tracker.removed = true;
			// Defer the splice during dispatch so the in-progress index walk isn't disturbed.
			if (dispatching) {
				hasPendingRemovals = true;
			} else {
				const index = trackers.indexOf(tracker);
				if (index >= 0) {
					trackers.splice(index, 1);
				}
			}
		};
		const tracker: HookTracker<T> = { callback, removed: false, unhook };
		trackers.push(tracker);

		if (cleanupOnDestroy) {
			onDestroy(unhook);
		}

		return unhook;
	};

	const nextNotification = () => {
		const { promise, resolve } = Promise.withResolvers<T>();
		promiseResolvers.push(resolve);
		return promise;
	};

	return {
		notify,
		onNotified,
		nextNotification
	};
};
