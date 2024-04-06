class AddEditItemSlotTool {
    static slotTable: TableDataEditor<IItemSlot>;
    static renderParent: HTMLDivElement;
    static tableDiv: HTMLDivElement;
    static itemSelect: HTMLSelectElement;
    static itemCache = new DataCache(() => ApiRequest.get('/api/Items'));
    static items: IItem[];
    static slotTypeCache = new DataCache(() => ApiRequest.get('/api/Items/SlotTypes'));
    static itemModCache = new DataCache(() => ApiRequest.get('/api/ItemMods'));

    static async init(renderParent: HTMLDivElement, initialSelection?: number) {
        this.initCaches();
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

        this.items = await this.itemCache.data;
        if (!this.items[0]) {
            this.items = this.items.slice(1);
        }

        this.items.forEach(item => {
            const opt = document.createElement('option');
            opt.textContent = item.itemName;
            opt.value = item.itemId.toString();
            opt.selected = false;
            this.itemSelect.appendChild(opt);
        });
        const itemOpts = this.items.map(item => ({
            id: item.itemId,
            name: item.itemName
        }));
        const data = await Promise.all([
            this.slotTypeCache.data,
            this.itemModCache.data,
        ]);
        const slotTypeOpts = data[0].map(slotType => ({
            id: slotType.slotTypeId,
            name: slotType.slotTypeName
        }));
        const groupedMods = groupBy(data[1], i => i.slotTypeId.toString());
        const itemModOpts: { [key: number]: SelOption[] } = {};
        keys(groupedMods).forEach(key => {
            itemModOpts[Number(key)] = groupedMods[key].map(mod => ({
                id: mod.itemModId,
                name: mod.itemModName
            }));
        });
        this.itemSelect.addEventListener('change', async () => {
            AddEditItemSlotTool.tableDiv.hidden = false;
            const selected = AddEditItemSlotTool.itemSelect.selectedOptions[0];
            const itemId = Number(selected.value);
            const itemSlots = await ApiRequest.get('/api/Items/SlotsForItem', { itemId: itemId, refreshCache: true });

            this.slotTable = new TableDataEditor(itemSlots, AddEditItemSlotTool.tableDiv, "itemSlotId", {
                "itemId": (i: IItemSlot) => itemOpts,
                "slotTypeId": (i: IItemSlot) => slotTypeOpts,
                "guaranteedId": (i: IItemSlot) => itemModOpts[i.slotTypeId]
            }, {
                itemSlotId: 0,
                itemId: itemId,
                guaranteedId: 0,
                slotTypeId: 1,
                probability: 1.00
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

    static initCaches() {
        this.itemCache.data;
        this.slotTypeCache.data;
        this.itemModCache.data;
    }
}