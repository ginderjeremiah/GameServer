import { onDestroy } from 'svelte';

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

const tooltipsData = $state<TooltipData[]>([]);

export const tooltips = {
	get data() {
		// filter out any undefined entries that may exist due to the async nature of tooltip registration and unregistration
		return tooltipsData.filter((t) => t);
	}
};

let id = 1;

export const registerTooltipComponent = <T extends TooltipComponent>(component: () => T | undefined) => {
	const data = $state<TooltipData>({ id, component, visible: false, position: undefined });
	id++;

	const setTooltipPosition = (position: Position) => {
		data.position = position;
	};

	const showTooltip = () => {
		data.visible = true;
	};

	const hideTooltip = () => {
		data.visible = false;
	};

	tooltipsData.push(data);

	onDestroy(() => {
		const index = tooltipsData.findIndex((tData) => tData === data);
		tooltipsData.splice(index, 1);
	});

	return {
		setTooltipPosition,
		showTooltip,
		hideTooltip
	};
};
