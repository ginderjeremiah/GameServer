import { onDestroy } from 'svelte';
import { SvelteMap } from 'svelte/reactivity';

export interface TooltipComponent {
	getBaseNode: () => HTMLDivElement;
}

export interface TooltipData {
	id: number;
	component: () => TooltipComponent | undefined;
	position?: Position;
	visible: boolean;
}

export interface Position {
	x: number;
	y: number;
}

// Keyed by the tooltip's stable id rather than held in a reactive array. An
// array relied on `findIndex(... === data)` + `splice` to unregister, which is
// not robust when screens overlap during navigation (the new screen mounts its
// tooltips before the old screen's onDestroy runs): the interleaved push/splice
// on the reactive array dropped the wrong entries and left undefined holes,
// causing tooltips to disappear when switching between screens that both render
// them (e.g. Fight <-> Inventory). Removing by id makes unregistration
// reference-independent and order-independent.
const tooltipsData = new SvelteMap<number, TooltipData>();

export const tooltips = {
	get data() {
		return [...tooltipsData.values()];
	}
};

let id = 1;

export const registerTooltipComponent = <T extends TooltipComponent>(component: () => T | undefined) => {
	const tooltipId = id++;
	const data = $state<TooltipData>({ id: tooltipId, component, visible: false, position: undefined });

	const setTooltipPosition = (position: Position) => {
		data.position = position;
	};

	const showTooltip = () => {
		data.visible = true;
	};

	const hideTooltip = () => {
		data.visible = false;
	};

	tooltipsData.set(tooltipId, data);

	onDestroy(() => {
		tooltipsData.delete(tooltipId);
	});

	return {
		setTooltipPosition,
		showTooltip,
		hideTooltip
	};
};
