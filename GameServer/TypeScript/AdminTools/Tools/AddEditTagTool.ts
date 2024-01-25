class AddEditTagTool {
    static tagsTable: TableDataEditor<Tag>;
    static renderParent: HTMLDivElement;

    static async init(renderParent: HTMLDivElement) {
        this.tagsTable = new TableDataEditor(await ApiRequest.get('/api/Tags/Tags'), renderParent, "tagId", {});
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