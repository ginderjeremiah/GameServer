import { onDestroy } from 'svelte';
import { Action } from './types';

interface HookTracker<T extends any[]> {
	id: number;
	callback: Action<[...T, Action]>;
	unhook: Action;
}

export const createHook = <T extends any[] = []>() => {
	let nextId = 0;
	const trackers: HookTracker<T>[] = [];

	const notify = (...data: T) => {
		for (const tracker of trackers) {
			tracker.callback(...data, tracker.unhook);
		}
	};

	const onNotified = (callback: Action<[...T, Action]>, cleanupOnDestroy: boolean = true) => {
		const id = nextId++;
		const unhook = () => {
			const index = trackers.findIndex((t) => (t.id = id));
			if (index > 0) {
				trackers.splice(index, 1);
			}
		};
		trackers.push({ id, callback, unhook });
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
