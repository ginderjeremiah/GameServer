﻿import { TableDataEditor } from "../TableDataEditor";
import { ApiRequest } from "../../Shared/Api/ApiRequest";
import { IItemMod } from "../../Shared/Api/Types";

export class AddEditItemModTool {
    static modTable: TableDataEditor<IItemMod>;
    static renderParent: HTMLDivElement;

    static async init(renderParent: HTMLDivElement) {
        const data = await Promise.all([
            ApiRequest.get('/api/ItemMods', { refreshCache: true }),
            ApiRequest.get('/api/Items/SlotTypes')
        ]);
        const slotTypeOpts = data[1];
        const getSlotTypes = () => {
            return {options: slotTypeOpts};
        };

        this.modTable = new TableDataEditor(data[0], renderParent, {
            primaryKey: "id",
            selOptions: {slotTypeId: getSlotTypes },
            hiddenColumns: ["attributes"]
        });
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