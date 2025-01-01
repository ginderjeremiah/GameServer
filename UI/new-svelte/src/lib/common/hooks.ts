import { onDestroy } from 'svelte';
import { Action } from './types';

interface HookTracker<T extends any[]> {
	id: number;
	callback: Action<[...T, Action]>;
	unhook: Action;
}

export const createHook = <T extends any[] = []>() => {
	let nextId = 0;
	let promiseResolvers: Action<[T]>[] = [];
	const trackers: HookTracker<T>[] = [];

	const notify = (...data: T) => {
		for (const resolve of promiseResolvers) {
			resolve(data);
		}

		promiseResolvers = [];

		for (const tracker of trackers) {
			tracker.callback(...data, tracker.unhook);
		}
	};

	const onNotified = (callback: Action<[...T, Action]>, cleanupOnDestroy: boolean = true) => {
		const id = nextId++;
		const unhook = createUnhook(trackers, id);
		trackers.push({ id, callback, unhook });

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

function createUnhook<T extends any[]>(trackers: HookTracker<T>[], id: number) {
	return () => {
		const index = trackers.findIndex((t) => t.id === id);
		if (index >= 0) {
			trackers.splice(index, 1);
		}
	};
}
