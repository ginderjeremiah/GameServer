/// <reference path="../Abstract/Tooltippable.ts"/>
class Item extends Tooltippable implements IInventoryItem, IItem {
    inventoryItemId: number;
    playerId: number;
    rating: number;
    itemId: number;
    itemName: string;
    itemDesc: string;
    itemCategoryId: number;
    equipped: boolean;
    slotId: number;
    itemMods: ItemMod[];
    attributes: IItemAttribute[]

    constructor(invItem: IInventoryItem, itemData: IItem, itemModsData: IItemMod[]) {
        super();
        this.inventoryItemId = invItem.inventoryItemId;
        this.playerId = invItem.playerId;
        this.rating = invItem.rating;
        this.itemId = invItem.itemId;
        this.itemName = itemData.itemName;
        this.itemDesc = itemData.itemDesc;
        this.itemCategoryId = itemData.itemCategoryId;
        this.attributes = itemData.attributes
        this.equipped = invItem.equipped;
        this.slotId = invItem.slotId;
        this.itemMods = invItem.itemMods.map(invMod => new ItemMod(invMod, itemModsData[invMod.itemModId]));
    }

    updateTooltipData(tooltipTitle: HTMLHeadingElement, tooltipContent: HTMLDivElement, prevId: number): number {
        if (this.toolTipId !== prevId) {
            tooltipTitle.textContent = this.itemName; //set title to item name (key)
            tooltipContent.replaceChildren(); //clear current content

            const statsHeader = document.createElement("h3");
            statsHeader.className = "tooltipHeader";
            statsHeader.textContent = "Stats:";
            tooltipContent.appendChild(statsHeader);

            const statsList = document.createElement("ul");
            statsList.className = "tooltipList";
            /*Object.keys(this.item.Stats).forEach((stat) => {
                const listItem = document.createElement("li");
                listItem.textContent = stat + " +" + this.item.Stats[stat];
                statsList.appendChild(listItem);
            });*/
            tooltipContent.appendChild(statsList);

            const descHeader = document.createElement("h3");
            descHeader.className = "tooltipHeader";
            descHeader.textContent = "Description:";
            tooltipContent.appendChild(descHeader);

            const desc = document.createElement("p");
            desc.className = "tooltipText";
            desc.textContent = this.itemDesc;
            tooltipContent.appendChild(desc);

            const modsHeader = document.createElement("h3");
            statsHeader.className = "tooltipHeader";
            statsHeader.textContent = "Mods:";
            tooltipContent.appendChild(modsHeader);

            const modsList = document.createElement("ul");
            statsList.className = "tooltipList";
            this.itemMods.forEach((mod) => {
                const listItem = document.createElement("li");
                const nameSpan = document.createElement('span');
                nameSpan.style.fontWeight = "bold";
                nameSpan.textContent = mod.itemModName;
                const descSpan = document.createElement('span');
                descSpan.textContent = ": " + mod.itemModDesc;
                listItem.appendChild(nameSpan);
                listItem.appendChild(descSpan);
                statsList.appendChild(listItem);
            });
            tooltipContent.appendChild(modsList);
        }
        return this.toolTipId;
    }
}
