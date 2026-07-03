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
	const trackers: HookTracker<T>[] = [];

	const notify = (...data: T) => {
		// Iterate by index without allocating a snapshot (this is one of the hottest
		// game-loop paths). Removals and additions during dispatch are both deferred so
		// the index walk isn't disturbed: an unhook only tombstones (`removed`) instead
		// of splicing, and the snapshotted `count` excludes any subscriber a callback
		// appends mid-dispatch — keeping it from firing on an event that predates it.
		// Tombstones are compacted out once after dispatch; appends survive it.
		dispatching = true;
		const count = trackers.length;
		for (let i = 0; i < count; i++) {
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

	/**
	 * Subscribes `callback` to this hook and returns an unsubscribe function. The hook is a generic
	 * pub/sub primitive with no framework coupling: by default it wires NO Svelte lifecycle cleanup,
	 * so a module-level, async, or `setTimeout` caller can subscribe safely and is responsible for
	 * calling the returned unsubscribe itself.
	 *
	 * @param cleanupOnDestroy Opt in to auto-unsubscribe via Svelte's `onDestroy`. ONLY pass `true`
	 *   from within component initialization (a `.svelte` `<script>` top level or a function reached
	 *   synchronously from it) — `onDestroy` throws/no-ops outside that window. Callers outside a
	 *   component context must leave this off and manage teardown via the returned unsubscribe.
	 */
	const onNotified = (callback: Action<[...T, Action]>, cleanupOnDestroy: boolean = false) => {
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

	return {
		notify,
		onNotified
	};
};
