import { TableDataEditor } from "../TableDataEditor";
import { IItem } from "../../Shared/Api/Types";
import { ApiRequest } from "../../Shared/Api/ApiRequest";


export class AddEditItemTool {
    static itemTable: TableDataEditor<IItem>;
    static renderParent: HTMLDivElement;

    static async init(renderParent: HTMLDivElement) {
        const data = await Promise.all([
            ApiRequest.get('/api/ItemCategories'),
            ApiRequest.get('/api/Items')
        ])
        const itemCategoryOpts = data[0].map(cat => ({
            id: cat.id,
            name: cat.name
        }));
        const getItemCategories = (i: IItem) => {
            return {options: itemCategoryOpts};
        }
        this.itemTable = new TableDataEditor(data[1],
            renderParent,
            {
                primaryKey: "id",
                selOptions: { itemCategoryId: getItemCategories },
                hiddenColumns: ["attributes"]
            }
        );
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
            await ApiRequest.post('/api/AdminTools/AddEditItems', changes);
            AddEditItemTool.init(this.renderParent);
        }
    }
}