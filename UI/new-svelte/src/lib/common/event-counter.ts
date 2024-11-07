import { Action } from "$lib/common"

export const getEventCounter = (callback: Action<[number]>, eventsPerReport = 10) => {
   let count = 0;
   let lastCheck = performance.now();

   return () => {
      count++;
      if (count >= eventsPerReport) {
         const now = performance.now();
         const elapsedMs = (now - lastCheck);
         const eventsPerSecond = count * 1000 / elapsedMs;
         count = 0;
         lastCheck = now;
         callback(eventsPerSecond);
      }
   }
}