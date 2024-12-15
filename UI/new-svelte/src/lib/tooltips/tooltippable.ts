import { TooltipFactory } from './tooltip-factory';

export abstract class Tooltippable {
	toolTipId: number;

	constructor() {
		this.toolTipId = TooltipFactory.getId();
	}

	abstract updateTooltipData(
		tooltipTitle: HTMLHeadingElement,
		tooltipContent: HTMLDivElement,
		prevId: number
	): number;
}
