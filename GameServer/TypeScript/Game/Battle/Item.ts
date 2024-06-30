/// <reference path="../Abstract/Tooltippable.ts"/>
class Item extends Tooltippable implements IItem {
    id: number;
    rating: number;
    itemId: number;
    name: string;
    description: string;
    itemCategoryId: number;
    iconPath: string;
    equipped: boolean;
    inventorySlotNumber: number;
    itemMods: ItemMod[];
    attributes: IBattlerAttribute[];
    totalAttributes: BattleAttributes;

    constructor(invItem: IInventoryItem, itemData: IItem, itemModsData: IItemMod[]) {
        super();
        this.id = invItem.id;
        this.rating = invItem.rating;
        this.itemId = invItem.itemId;
        this.name = itemData.name;
        this.description = itemData.description;
        this.itemCategoryId = itemData.itemCategoryId;
        this.attributes = itemData.attributes;
        this.iconPath = itemData.iconPath;
        this.equipped = invItem.equipped;
        this.inventorySlotNumber = invItem.inventorySlotNumber;
        this.itemMods = invItem.itemMods.map(invMod => new ItemMod(invMod, itemModsData[invMod.itemModId]));
        const itemModAttributes = this.itemMods.flatMap(mod => mod.attributes);
        this.totalAttributes = new BattleAttributes([...this.attributes, ...itemModAttributes], false);
    }

    updateTooltipData(tooltipTitle: HTMLHeadingElement, tooltipContent: HTMLDivElement, prevId: number): number {
        if (this.toolTipId !== prevId) {
            tooltipTitle.textContent = this.name; //set title to item name (key)
            tooltipContent.replaceChildren(); //clear current content

            const statsHeader = document.createElement("h3");
            statsHeader.className = "tooltipHeader";
            statsHeader.textContent = "Stats:";
            tooltipContent.appendChild(statsHeader);

            const statsList = document.createElement("ul");
            statsList.className = "tooltipList";
            this.totalAttributes.getAttributeMap().forEach((att) => {
                const listItem = document.createElement("li");
                listItem.textContent = att.name + " +" + att.value;
                statsList.appendChild(listItem);
            });
            tooltipContent.appendChild(statsList);

            if (this.itemMods.length > 0) {
                const modsHeader = document.createElement("h3");
                modsHeader.className = "tooltipHeader";
                modsHeader.textContent = "Mods:";
                tooltipContent.appendChild(modsHeader);

                const modsList = document.createElement("ul");
                modsList.className = "tooltipList";
                this.itemMods.forEach((mod) => {
                    const listItem = document.createElement("li");
                    const nameSpan = document.createElement('span');
                    nameSpan.style.fontWeight = "bold";
                    nameSpan.textContent = mod.name;
                    const descSpan = document.createElement('span');
                    descSpan.textContent = ": " + mod.description;
                    listItem.appendChild(nameSpan);
                    listItem.appendChild(descSpan);
                    modsList.appendChild(listItem);
                });
                tooltipContent.appendChild(modsList);
            }

            const descHeader = document.createElement("h3");
            descHeader.className = "tooltipHeader";
            descHeader.textContent = "Description:";
            tooltipContent.appendChild(descHeader);

            const desc = document.createElement("p");
            desc.className = "tooltipText";
            desc.textContent = this.description;
            tooltipContent.appendChild(desc);
        }
        return this.toolTipId;
    }
}
