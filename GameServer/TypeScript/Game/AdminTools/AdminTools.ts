import { AddEditItemAttributeTool } from "./Tools/AddEditItemAttributeTool";
import { AddEditItemModAttributeTool } from "./Tools/AddEditItemModAttributeTool";
import { AddEditItemTool } from "./Tools/AddEditItemTool";
import { AddEditItemModTool } from "./Tools/AddEditItemModTool";
import { AddEditItemSlotTool } from "./Tools/AddEditItemSlotTool";
import { AddEditTagTool } from "./Tools/AddEditTagTool";
import { SetItemTagsTool } from "./Tools/SetItemTagsTool";
import { SetItemModTagsTool } from "./Tools/SetItemComponentTagsTool";
import { keys } from "../Shared/GlobalFunctions";

export class AdminTools {
    static adminToolSelect: HTMLSelectElement;
    static renderParent: HTMLDivElement;
    static adminTools: { [key: string]: (d: HTMLDivElement) => void } = {
        "Add/Edit Items": (d: HTMLDivElement) => AddEditItemTool.init(d),
        "Add/Edit Item Attributes": (d: HTMLDivElement) => AddEditItemAttributeTool.init(d),
        "Add/Edit Item Mods": (d: HTMLDivElement) => AddEditItemModTool.init(d),
        "Add/Edit Item Mod Attributes": (d: HTMLDivElement) => AddEditItemModAttributeTool.init(d),
        "Add/Edit Item Slots": (d: HTMLDivElement) => AddEditItemSlotTool.init(d),
        "Add/Edit Tags": (d: HTMLDivElement) => AddEditTagTool.init(d),
        "Set Item Tags": (d: HTMLDivElement) => SetItemTagsTool.init(d),
        "Set Item Component Tags": (d: HTMLDivElement) => SetItemModTagsTool.init(d)
    }

    static init() {
        const selectLabel = document.createElement('label');
        selectLabel.htmlFor = 'adminToolSelect';
        selectLabel.textContent = 'Select Admin Tool: ';
        document.body.appendChild(selectLabel);

        this.adminToolSelect = document.createElement('select');
        this.adminToolSelect.multiple = false;
        this.adminToolSelect.style.marginBottom = '2em';
        const blankOpt = document.createElement('option');
        blankOpt.selected = true;
        blankOpt.disabled = true;
        blankOpt.hidden = true;
        this.adminToolSelect.appendChild(blankOpt);
        keys(this.adminTools).forEach(key => {
            const opt = document.createElement('option');
            opt.textContent = key;
            opt.value = key;
            opt.selected = false;
            this.adminToolSelect.appendChild(opt);
        });
        document.body.appendChild(this.adminToolSelect);

        this.renderParent = document.createElement('div');
        this.renderParent.id = 'renderParent';
        this.renderParent.hidden = true;
        this.renderParent.style.border = '2px solid black';
        this.renderParent.style.padding = '1em';
        document.body.appendChild(this.renderParent);

        this.adminToolSelect.addEventListener('change', () => {
            AdminTools.renderParent.hidden = false;
            const selected = AdminTools.adminToolSelect.selectedOptions[0];
            AdminTools.adminTools[selected.value](AdminTools.renderParent);
        });
    }
}

AdminTools.init();