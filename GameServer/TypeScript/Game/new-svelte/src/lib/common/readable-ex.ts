import { Readable, readable } from "svelte/store"

export interface ReadableEx<T> extends Readable<T> {
   get value(): T;
   refresh: () => void;
}

export const readableEx = <T>(value?: T, setCapturer?: (set: (value: T) => void) => void) => {
   let setEx: (value: T) => void;
   const base = readable<T>(value, (set) => {
      setEx = set;
      setCapturer?.(set)
   });
   let currentValue = value as T;
   const extended = {
      get value() {
         return currentValue;
      },
      subscribe: base.subscribe,
      refresh: () => setEx(currentValue)
   };
   return extended as ReadableEx<T>;
}