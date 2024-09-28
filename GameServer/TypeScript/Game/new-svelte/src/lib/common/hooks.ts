import { onDestroy } from "svelte";
import { Action } from "./types";

interface HookTracker<T> {
   id: number;
   callback: Action<T>;
}

export const createHook = <T = void>() => {
   let currentId = 0;
   const trackers: HookTracker<T>[] = [];

   const notify = (data: T) => {
      for (const tracker of trackers) {
         tracker.callback(data);
      }
   }

   const onNotified = (callback: Action<T>) => {
      const id = currentId++;
      trackers.push({ id, callback });
      onDestroy(() => {
         const index = trackers.findIndex(t => t.id = id);
         if (index > 0) {
            trackers.splice(index, 1);
         }
      });
   }

   return {
      notify,
      onNotified,
   };
}