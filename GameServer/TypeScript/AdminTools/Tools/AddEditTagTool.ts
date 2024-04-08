class AddEditTagTool {
    static tagsTable: TableDataEditor<ITag>;
    static renderParent: HTMLDivElement;

    static async init(renderParent: HTMLDivElement) {
        const data = await Promise.all([
            ApiRequest.get('/api/Tags'),
            ApiRequest.get('/api/Tags/TagCategories')
        ])
        const tagCategoryOpts = data[1].map(cat => ({
            id: cat.tagCategoryId,
            name: cat.tagCategoryName
        }));
        this.tagsTable = new TableDataEditor(data[0], renderParent, {
            primaryKey: "tagId",
            selOptions: {"tagCategoryId": () => ({options: tagCategoryOpts})}
        });
        this.renderParent = renderParent;
        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            AddEditTagTool.submit();
        });
        renderParent.appendChild(submitButton);
    }
     
    static async submit() {
        const changes = this.tagsTable.getChanges();
        if (changes.length > 0) {
            await new ApiRequest('/api/AdminTools/AddEditTags').post(changes);
            AddEditTagTool.init(this.renderParent);
        }
    }
}