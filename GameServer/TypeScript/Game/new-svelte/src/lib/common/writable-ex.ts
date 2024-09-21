import { Writable, writable } from "svelte/store"

export interface WritableEx<T> extends Writable<T> {
   get value(): T;
   refresh: () => void;
}

export const writableEx = <T>(value?: T) => {
   const base = writable<T>(value);
   let currentValue = value as T;
   const extended = {
      get value() {
         return currentValue;
      },
      set: (value: T) => {
         currentValue = value;
         base.set(value);
      },
      subscribe: base.subscribe,
      update: (updater: (value: T) => T) => {
         currentValue = updater(currentValue);
         base.set(currentValue!);
      },
      refresh: () => base.set(currentValue)
   };

   return extended as WritableEx<T>;
}