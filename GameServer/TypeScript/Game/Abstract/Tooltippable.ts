abstract class Tooltippable {
    toolTipId: number;

    constructor() {
        this.toolTipId = TooltipManager.getId();
    }

    abstract updateTooltipData(tooltipTitle: HTMLHeadingElement, tooltipContent: HTMLDivElement, prevId: number): number;
}