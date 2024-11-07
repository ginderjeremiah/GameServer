import { TableDataEditor } from "../TableDataEditor";
import { IItemSlot, IItem, ISlotType, IItemMod } from "../../Shared/Api/Types";
import { ApiRequest } from "../../Shared/Api/ApiRequest";
import { groupBy, keys } from "../../Shared/GlobalFunctions";
import { SelOptions } from "../../Shared/GlobalInterfaces";

export class AddEditItemSlotTool {
    static slotTable: TableDataEditor<IItemSlot>;
    static renderParent: HTMLDivElement;
    static tableDiv: HTMLDivElement;
    static itemSelect: HTMLSelectElement;
    static items: IItem[];
    static slotTypes: ISlotType[];
    static itemMods: IItemMod[];

    static async init(renderParent: HTMLDivElement, initialSelection?: number) {
        renderParent.replaceChildren();

        const selectLabel = document.createElement('label');
        selectLabel.htmlFor = 'itemSelect';
        selectLabel.textContent = 'Select Item: ';
        renderParent.appendChild(selectLabel);

        this.itemSelect = document.createElement('select');
        this.itemSelect.multiple = false;
        this.itemSelect.style.marginBottom = '2em';
        const blankOpt = document.createElement('option');
        blankOpt.selected = true;
        blankOpt.disabled = true;
        blankOpt.hidden = true;
        this.itemSelect.appendChild(blankOpt);
        renderParent.appendChild(this.itemSelect);

        this.tableDiv = document.createElement('div');
        this.tableDiv.id = 'tableDiv';
        this.tableDiv.hidden = true;
        this.tableDiv.style.border = '2px solid black';
        this.tableDiv.style.padding = '1em';
        renderParent.appendChild(this.tableDiv);
        const data = await Promise.all([
            ApiRequest.get('/api/Items'),
            ApiRequest.get('/api/Items/SlotTypes'),
            ApiRequest.get('/api/ItemMods')
        ]);
        this.items = data[0];
        if (!this.items[0]) {
            this.items = this.items.slice(1);
        }

        this.items.forEach(item => {
            const opt = document.createElement('option');
            opt.textContent = item.name;
            opt.value = item.id.toString();
            opt.selected = false;
            this.itemSelect.appendChild(opt);
        });
        const itemOpts = this.items;
        this.slotTypes = data[1];
        this.itemMods = data[2];
        const slotTypeOpts = this.slotTypes;
        const groupedMods = groupBy(this.itemMods, i => i.slotTypeId.toString());
        const itemModOpts: { [key: number]: SelOptions } = {};
        keys(groupedMods).forEach(key => {
            itemModOpts[Number(key)] = {
                allowBlanks: true,
                options: groupedMods[key].map(mod => ({
                    id: mod.id,
                    name: mod.name
                }))
            };
        });
        this.itemSelect.addEventListener('change', async () => {
            AddEditItemSlotTool.tableDiv.hidden = false;
            const selected = AddEditItemSlotTool.itemSelect.selectedOptions[0];
            const itemId = Number(selected.value);
            const itemSlots = await ApiRequest.get('/api/Items/SlotsForItem', { itemId: itemId, refreshCache: true });

            this.slotTable = new TableDataEditor(itemSlots, AddEditItemSlotTool.tableDiv, {
                primaryKey: "id",
                selOptions: {
                    "itemId": () => ({options: itemOpts}),
                    "slotTypeId": () => ({options: slotTypeOpts}),
                    "guaranteedId": (i: IItemSlot) => itemModOpts[i.slotTypeId]
                },
                sampleItem: {
                    id: 0,
                    itemId: itemId,
                    slotTypeId: 1,
                    guaranteedItemModId: 0,
                    probability: 1.00
                }
            });
        });

        this.renderParent = renderParent;
        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            AddEditItemSlotTool.submit();
        });
        renderParent.appendChild(submitButton);

        if (initialSelection) {
            this.itemSelect.selectedIndex = initialSelection;
            this.itemSelect.dispatchEvent(new Event('change'));
        }
    }
     
    static async submit() {
        const changes = this.slotTable.getChanges();
        if (changes.length > 0) {
            await new ApiRequest('/api/AdminTools/AddEditItemSlots').post(changes);
            const selectedIndex = AddEditItemSlotTool.itemSelect.selectedOptions[0].index;
            AddEditItemSlotTool.init(this.renderParent, selectedIndex);
        }
    }
}