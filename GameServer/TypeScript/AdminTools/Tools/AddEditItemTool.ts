class AddEditItemTool {
    static itemTable: TableDataEditor<ItemData>;
    static renderParent: HTMLDivElement;

    static async init(renderParent: HTMLDivElement) {
        const itemCategoryOpts = (await ApiRequest.get('/api/ItemCategory/ItemCategories')).map(cat => ({
            id: cat.itemCategoryId,
            name: cat.categoryName
        }));
        const getItemCategories = (i: ItemData) => {
            return itemCategoryOpts;
        }
        this.itemTable = new TableDataEditor(await ApiRequest.get('/api/Item/Items'), renderParent, "itemId", { "itemCategoryId": getItemCategories });
        this.renderParent = renderParent;
        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            AddEditItemTool.submit();
        });
        renderParent.appendChild(submitButton);
    }
     
    static async submit() {
        const changes = this.itemTable.getChanges();
        if (changes.length > 0) {
            await new ApiRequest('/api/AdminTools/AddEditItems').post(changes);
            AddEditItemTool.init(this.renderParent);
        }
    }
}