import { writableEx, createHook, getEventCounter } from "$lib/common";
import { logicalTime } from "./game-engine";

export let renderDelta = writableEx(0);
export let renderTickRate = writableEx(0);

const renderUpdateHook = createHook<number>();
const notifyRenderUpdate = renderUpdateHook.notify;
export const onRenderUpdate = renderUpdateHook.onNotified;

let countTick = getEventCounter(t => renderTickRate.set(Math.round(t)));
let initialized = false;

export const startRenderEngine = () => {
   if (!initialized) {
      initialized = true;
      window.requestAnimationFrame(renderLoop);
   }
};

//use performace.now instead of animation frame ts, because frame ts is before some amount of processing.
//using frame ts can cause render loop to be behind logical loop
const renderLoop = () => {
   update(performance.now());
   window.requestAnimationFrame(renderLoop);
}

const update = (ts: DOMHighResTimeStamp) => {
   countTick();
   const delta = ts - logicalTime;
   renderDelta.set(delta);
   notifyRenderUpdate(delta);
}
