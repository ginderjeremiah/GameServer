import { delay } from "./functions";
import { Action } from "./types";

export class DelayedAction {
   #action: Action<void>
   #delayLength: number
   #triggerStartTime: number

   constructor(delayInMs: number, action: Action<void>) {
      this.#delayLength = delayInMs;
      this.#action = action;
      this.#triggerStartTime = 0;
   }

   async start() {
      this.#triggerStartTime = performance.now();
      await delay(this.#delayLength);
      const now = performance.now();
      if (now - this.#triggerStartTime >= this.#delayLength * 0.95) {
         this.#triggerStartTime = now;
         this.#action();
      }
   }

   cancel() {
      this.#triggerStartTime = performance.now() + this.#delayLength;
   }
}