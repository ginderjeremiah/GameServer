/// <reference path="../Abstract/Tooltippable.ts"/>
class Item extends Tooltippable implements InventoryItem, ItemData {
    inventoryItemId: number;
    playerId: number;
    rating: number;
    itemId: number;
    itemName: string;
    itemDesc: string;
    itemCategoryId: number;
    equipped: boolean;
    slotId: number;
    itemMods: InventoryItemMod[];

    constructor(invItem: InventoryItem, itemData: ItemData) {
        super();
        this.inventoryItemId = invItem.inventoryItemId;
        this.playerId = invItem.playerId;
        this.rating = invItem.rating;
        this.itemId = invItem.itemId;
        this.itemName = itemData.itemName;
        this.itemDesc = itemData.itemDesc;
        this.itemCategoryId = itemData.itemCategoryId;
        this.equipped = invItem.equipped;
        this.slotId = invItem.slotId;
        this.itemMods = invItem.itemMods;
    }

    updateTooltipData(tooltipTitle: HTMLHeadingElement, tooltipContent: HTMLDivElement, prevId: number): number {
        if (this.toolTipId !== prevId) {
            tooltipTitle.textContent = this.itemName; //set title to item name (key)
            tooltipContent.replaceChildren(); //clear current content

            let statsHeader = document.createElement("h3");
            statsHeader.className = "tooltipHeader";
            statsHeader.textContent = "Stats:";
            tooltipContent.appendChild(statsHeader);

            let statsList = document.createElement("ul");
            statsList.className = "tooltipList";
            /*Object.keys(this.item.Stats).forEach((stat) => {
                let listItem = document.createElement("li");
                listItem.textContent = stat + " +" + this.item.Stats[stat];
                statsList.appendChild(listItem);
            });*/
            tooltipContent.appendChild(statsList);

            let descHeader = document.createElement("h3");
            descHeader.className = "tooltipHeader";
            descHeader.textContent = "Description:";
            tooltipContent.appendChild(descHeader);

            let desc = document.createElement("p");
            desc.className = "tooltipText";
            desc.textContent = this.itemDesc;
            tooltipContent.appendChild(desc);
        }
        return this.toolTipId;
    }
}
