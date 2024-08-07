﻿import { ITag, IItemMod } from "../../Shared/Api/Types";
import { ApiRequest } from "../../Shared/Api/ApiRequest";

export class SetItemModTagsTool {
    static sel: HTMLSelectElement;
    static checkDiv: HTMLDivElement;
    static items: IItemMod[];
    static tags: ITag[];
    static selectedItemId: number;

    static async init(renderParent: HTMLDivElement) {
        const reqs = Promise.all([
            ApiRequest.get('/api/ItemMods'),
            ApiRequest.get('/api/Tags')
        ]);

        this.initHtml(renderParent);

        const res = await reqs;
        this.items = res[0];
        this.tags = res[1];
        this.items.splice(1).forEach(item => {
            const selOption = document.createElement('option');
            selOption.value = item.id.toString();
            selOption.text = item.name;
            this.sel.appendChild(selOption);
        });
        this.tags.forEach(tag => {
            const checkBox = document.createElement('input');
            checkBox.type = 'checkbox';
            checkBox.id = 'checkbox' + tag.id;
            checkBox.value = tag.id.toString();
            const checkLabel = document.createElement('label');
            checkLabel.htmlFor = 'checkbox' + tag.id;
            checkLabel.textContent = tag.name;
            this.checkDiv.appendChild(checkBox);
            this.checkDiv.appendChild(checkLabel);
            this.checkDiv.appendChild(document.createElement('br'))
        })

        this.sel.addEventListener('change', (ev) => {
            const selected = SetItemModTagsTool.sel.selectedOptions[0];
            SetItemModTagsTool.setCheckBoxes(selected.value);
        });
    }

    static initHtml(renderParent: HTMLDivElement) {
        renderParent.replaceChildren();

        const itemsLabel = document.createElement('label');
        itemsLabel.htmlFor = 'itemsDDL';
        itemsLabel.textContent = 'Select Item: ';
        renderParent.appendChild(itemsLabel);

        this.sel = document.createElement('select');
        this.sel.id = 'itemsDDL';
        this.sel.style.marginBottom = '1em';

        const blankOpt = document.createElement('option');
        blankOpt.selected = true;
        blankOpt.disabled = true;
        blankOpt.hidden = true;
        this.sel.appendChild(blankOpt);

        renderParent.appendChild(this.sel);

        this.checkDiv = document.createElement('div');
        this.checkDiv.id = 'checkboxDiv';
        renderParent.appendChild(this.checkDiv);

        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            SetItemModTagsTool.submit();
        });
        renderParent.appendChild(submitButton);
    } 

    static async setCheckBoxes(itemId: string) {
        this.selectedItemId = Number(itemId);
        const selectedTags = ApiRequest.get('/api/Tags/TagsForItemMod', { itemModId: this.selectedItemId });
        for (const child of this.checkDiv.getElementsByTagName('input')) {
            child.checked = false;
        }
        (await selectedTags).forEach(tag => {
            const checkB = document.getElementById('checkbox' + tag.id) as HTMLInputElement;
            checkB.checked = true;
        });
    }

    static async submit() {
        const selectedTagIds: number[] = [];
        for (const child of this.checkDiv.getElementsByTagName('input')) {
            if (child.checked) {
                selectedTagIds.push(Number(child.value));
            }
        }
        new ApiRequest('/api/AdminTools/SetTagsForItemMod').post({ id: this.selectedItemId, tagIds: selectedTagIds });
    }
}