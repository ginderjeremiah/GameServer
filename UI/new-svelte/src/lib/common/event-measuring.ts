import { Action } from '$lib/common';

export const getEventCounter = (callback: Action<[number]>, eventsPerReport = 10) => {
	let count = 0;
	let lastCheck = performance.now();

	return () => {
		count++;
		if (count >= eventsPerReport) {
			const now = performance.now();
			const elapsedMs = now - lastCheck;
			const eventsPerSecond = (count * 1000) / elapsedMs;
			count = 0;
			lastCheck = now;
			callback(eventsPerSecond);
		}
	};
};

export const getEventAverager = (callback: Action<[number]>, eventsPerReport = 10, recencyBias = 0.5) => {
	const eventHistory: { value: number; timestamp: number }[] = [];

	return (value: number) => {
		const now = performance.now();
		eventHistory.push({ value, timestamp: now });
		if (eventHistory.length > eventsPerReport) {
			eventHistory.shift();
		}

		const average = getBiasWeightedAverage(eventHistory, recencyBias);

		callback(average);
	};
};

// This was AI generated... I did not check the math on this.
const getBiasWeightedAverage = (values: { value: number; timestamp: number }[], recencyBias: number) => {
	const now = performance.now();
	const totalWeight = values.reduce((sum, val) => sum + Math.pow(1 - recencyBias, (now - val.timestamp) / 1000), 0);
	return values.reduce(
		(sum, val) => sum + (val.value * Math.pow(1 - recencyBias, (now - val.timestamp) / 1000)) / totalWeight,
		0
	);
};
