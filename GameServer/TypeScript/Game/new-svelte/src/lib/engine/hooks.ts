import { Action } from "$lib/common";
import { onDestroy } from "svelte";

export type Hooks = {
   'update': number;
   'battle-start': never;
}

type ValueHooks = { [Key in keyof Hooks]: Hooks[Key] extends never ? never : Key }[keyof Hooks]

interface Tracker<T extends keyof Hooks> {
   id: number;
   callback: Action<Hooks[T]>;
}

type HookTrackers = {
   [key in keyof Hooks]: Tracker<key>[];
}

const hooks: Partial<HookTrackers> = {};
let currentId = 0;

export const registerHook = <T extends keyof Hooks>(name: T, callback: Action<Hooks[T]>) => {
   hooks[name] ??= [];
   const id = currentId++;
   hooks[name].push({ id, callback });
   onDestroy(() => {
      const trackers = hooks[name];
      if (trackers) {
         const index = trackers.findIndex(t => t.id === id);
         if (index > 0) {
            trackers.splice(index, 1);
         }
      }
   });
}

export function triggerHook<T extends keyof Exclude<Hooks, ValueHooks>>(name: T): void
export function triggerHook<T extends ValueHooks>(name: T, data: Hooks[T]): void
export function triggerHook<T extends keyof Hooks>(name: T, data?: any): void {
   const trackers = hooks[name];
   if (trackers) {
      for (const { callback } of trackers) {
         callback(data);
      }
   }
}