class AddEditItemModTool {
    static modTable: TableDataEditor<ItemMod>;
    static renderParent: HTMLDivElement;

    static async init(renderParent: HTMLDivElement) {
        const data = await Promise.all([
            ApiRequest.get('/api/ItemMod/ItemMods'),
            ApiRequest.get('/api/Item/SlotTypes')
        ]);
        const slotTypeOpts = data[1].map(slotType => ({
            id: slotType.slotTypeId,
            name: slotType.slotTypeName
        }));
        const getSlotTypes = () => {
            return slotTypeOpts;
        };

        this.modTable = new TableDataEditor(data[0], renderParent, "itemModId", { slotTypeId: getSlotTypes });
        this.renderParent = renderParent;
        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            AddEditItemModTool.submit();
        });
        renderParent.appendChild(submitButton);
    }

    static async submit() { 
        const changes = this.modTable.getChanges();
        if (changes.length > 0) {
            await new ApiRequest('/api/AdminTools/AddEditItemMods').post(changes);
            AddEditItemModTool.init(this.renderParent);
        }
    }
}