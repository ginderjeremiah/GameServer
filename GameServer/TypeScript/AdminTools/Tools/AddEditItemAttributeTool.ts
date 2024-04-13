class AddEditItemAttributeTool {
    static slotTable: TableDataEditor<IBattlerAttribute>;
    static renderParent: HTMLDivElement;
    static tableDiv: HTMLDivElement;
    static itemSelect: HTMLSelectElement;
    static items: IItem[];

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

        this.items = await ApiRequest.get('/api/Items');
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

        const attributeOpts = enumPairs(AttributeType).map(pair => ({id: pair.id, name: normalizeText(pair.name)}));

        this.itemSelect.addEventListener('change', async () => {
            AddEditItemAttributeTool.tableDiv.hidden = false;
            const selected = AddEditItemAttributeTool.itemSelect.selectedOptions[0];
            const itemId = Number(selected.value);

            this.slotTable = new TableDataEditor(this.items[itemId].attributes, AddEditItemAttributeTool.tableDiv, {
                selOptions: {
                    "attributeId": () => ({options: attributeOpts})
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
            AddEditItemAttributeTool.submit();
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
            const selected = AddEditItemAttributeTool.itemSelect.selectedOptions[0];
            const itemId = Number(selected.value);
            await new ApiRequest('/api/AdminTools/AddEditItemAttributes').post({itemId, changes});
            const selectedIndex = AddEditItemAttributeTool.itemSelect.selectedOptions[0].index;
            AddEditItemAttributeTool.init(this.renderParent, selectedIndex);
        }
    }
}