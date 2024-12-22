import { createHook, getEventCounter } from '$lib/common';
import { logicalState } from './game-engine.svelte';

let renderDelta = $state(0);
let renderTickRate = $state(0);

export const renderState = {
	get delta() {
		return renderDelta;
	},
	get tickRate() {
		return renderTickRate;
	}
};

const renderUpdateHook = createHook<[number]>();
const notifyRenderUpdate = renderUpdateHook.notify;
export const onRenderUpdate = renderUpdateHook.onNotified;

let countTick = getEventCounter((t) => (renderTickRate = Math.round(t)));
let initialized = false;

export const initRenderEngine = () => {
	if (!initialized) {
		initialized = true;
		window.requestAnimationFrame(renderLoop);
	}
};

//use performace.now instead of animation frame timestamp, because frame stamp is before some amount of processing.
//using frame timestamp can cause render loop to be behind logical loop
const renderLoop = () => {
	update(performance.now());
	window.requestAnimationFrame(renderLoop);
};

const update = (ts: DOMHighResTimeStamp) => {
	countTick();
	const delta = ts - logicalState.time;
	renderDelta = delta;
	notifyRenderUpdate(delta);
};
