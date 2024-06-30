class AddEditItemModAttributeTool {
    static slotTable: TableDataEditor<IBattlerAttribute>;
    static renderParent: HTMLDivElement;
    static tableDiv: HTMLDivElement;
    static itemModSelect: HTMLSelectElement;
    static itemMods: IItemMod[];

    static async init(renderParent: HTMLDivElement, initialSelection?: number) {
        renderParent.replaceChildren();

        const selectLabel = document.createElement('label');
        selectLabel.htmlFor = 'itemModSelect';
        selectLabel.textContent = 'Select Item Mod: ';
        renderParent.appendChild(selectLabel);

        this.itemModSelect = document.createElement('select');
        this.itemModSelect.multiple = false;
        this.itemModSelect.style.marginBottom = '2em';
        const blankOpt = document.createElement('option');
        blankOpt.selected = true;
        blankOpt.disabled = true; 
        blankOpt.hidden = true;
        this.itemModSelect.appendChild(blankOpt);
        renderParent.appendChild(this.itemModSelect);

        this.tableDiv = document.createElement('div');
        this.tableDiv.id = 'tableDiv';
        this.tableDiv.hidden = true;
        this.tableDiv.style.border = '2px solid black';
        this.tableDiv.style.padding = '1em';
        renderParent.appendChild(this.tableDiv);

        this.itemMods = await ApiRequest.get('/api/ItemMods', {refreshCache: true});
        if (!this.itemMods[0]) {
            this.itemMods = this.itemMods.slice(1);
        }

        this.itemMods.forEach(itemMod => {
            const opt = document.createElement('option');
            opt.textContent = itemMod.name;
            opt.value = itemMod.id.toString();
            opt.selected = false;
            this.itemModSelect.appendChild(opt);
        });

        const attributeOpts = enumPairs(AttributeType).map(pair => ({id: pair.id, name: normalizeText(pair.name)}));

        this.itemModSelect.addEventListener('change', async () => {
            AddEditItemModAttributeTool.tableDiv.hidden = false;
            const selected = AddEditItemModAttributeTool.itemModSelect.selectedOptions[0];
            const itemModId = Number(selected.value);

            this.slotTable = new TableDataEditor(this.itemMods[itemModId].attributes, AddEditItemModAttributeTool.tableDiv, {
                selOptions: {
                    attributeId: () => ({options: attributeOpts})
                }, 
                sampleItem: {
                    attributeId: AttributeType.Strength,
                    amount: 1.00
                }
            });
        });

        this.renderParent = renderParent;
        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            AddEditItemModAttributeTool.submit();
        });
        renderParent.appendChild(submitButton);

        if (initialSelection) {
            this.itemModSelect.selectedIndex = initialSelection;
            this.itemModSelect.dispatchEvent(new Event('change'));
        }
    }
     
    static async submit() {
        const changes = this.slotTable.getChanges();
        if (changes.length > 0) {
            const selected = AddEditItemModAttributeTool.itemModSelect.selectedOptions[0];
            const itemModId = Number(selected.value);
            await new ApiRequest('/api/AdminTools/AddEditItemModAttributes').post({itemModId, changes});
            const selectedIndex = AddEditItemModAttributeTool.itemModSelect.selectedOptions[0].index;
            AddEditItemModAttributeTool.init(this.renderParent, selectedIndex);
        }
    }
}