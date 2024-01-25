"use strict";
class ApiRequest {
    r;
    endpoint;
    constructor(endpoint) {
        this.r = new XMLHttpRequest();
        this.endpoint = endpoint;
    }
    get(urlParams) {
        const params = this.encodeParams(urlParams);
        const endpoint = params
            ? this.endpoint + '?' + params
            : this.endpoint;
        this.r.open('GET', endpoint, true);
        const r = this.r;
        return new Promise((resolved, rejected) => {
            r.onload = (ev) => resolved(new ApiResponse(r, ev));
            r.onerror = (ev) => rejected(new ApiResponse(r, ev));
            r.onabort = r.onerror;
            r.send();
        });
    }
    static get(endpoint, urlParams) {
        const request = new ApiRequest(endpoint);
        return new Promise(async (resolved) => resolved((await request.get(urlParams)).data));
    }
    post(payload) {
        this.r.open('POST', this.endpoint, true);
        const r = this.r;
        r.setRequestHeader('content-type', 'application/json');
        const p = payload;
        return new Promise((resolved, rejected) => {
            r.onload = (ev) => resolved(new ApiResponse(r, ev));
            r.onerror = (ev) => rejected(new ApiResponse(r, ev));
            r.onabort = r.onerror;
            r.send(JSON.stringify(p));
        });
    }
    static post(endpoint, payload) {
        const request = new ApiRequest(endpoint);
        return new Promise(async (resolved) => resolved((await request.post(payload)).data));
    }
    encodeParams(urlParams) {
        return keys(urlParams)
            .filter(key => urlParams[key] !== undefined)
            .map(key => key + '=' + window.encodeURIComponent(urlParams[key]))
            .join('&');
    }
}
function IsEnemyInstance(instance) {
    return instance.seed !== undefined;
}
/// <reference path="Shared/Api/ApiRequest.ts"/>
/// <reference path="Shared/interfaces.ts"/>
class AdminTools {
    static adminToolSelect;
    static renderParent;
    static adminTools = {
        "Add/Edit Tags": (d) => AddEditTagTool.init(d),
        "Add/Edit Item Mods": (d) => AddEditItemModTool.init(d),
        "Add/Edit Item Slots": (d) => AddEditItemSlotTool.init(d),
        "Add/Edit Items": (d) => AddEditItemTool.init(d),
        "Set Item Tags": (d) => SetItemTagsTool.init(d),
        "Set Item Component Tags": (d) => SetItemModTagsTool.init(d)
    };
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
class TableDataEditor {
    dataOriginals;
    dataFinals;
    table;
    sampleItem;
    keys;
    selOptions;
    primaryKey;
    newItemIndex = -1;
    constructor(data, parent, primaryKey, selOptions, sampleItem) {
        parent.replaceChildren();
        if (data.length > 0 && !data[0]) {
            data = data.splice(1);
        }
        this.primaryKey = primaryKey;
        this.selOptions = selOptions;
        this.table = document.createElement('table');
        this.dataOriginals = JSON.parse(JSON.stringify(data));
        this.dataFinals = data;
        this.sampleItem = sampleItem ?? data[0];
        this.keys = keys(this.sampleItem);
        const headerRow = document.createElement('tr');
        this.keys.forEach(key => headerRow.appendChild(this.createHeader(key)));
        const delHeader = document.createElement('th');
        delHeader.textContent = 'Delete';
        headerRow.appendChild(delHeader);
        this.table.appendChild(headerRow);
        data.forEach(t => this.table.appendChild(this.createDataRow(t)));
        parent.appendChild(this.table);
        const addRowButton = document.createElement('button');
        addRowButton.textContent = 'Add Row';
        addRowButton.style.marginTop = '1em';
        addRowButton.addEventListener('click', () => {
            const newItem = {};
            this.keys.forEach(key => {
                if (key == this.primaryKey) {
                    newItem[this.primaryKey] = this.newItemIndex--;
                }
                else {
                    newItem[key] = this.sampleItem[key];
                }
            });
            this.dataFinals.push(newItem);
            this.table.appendChild(this.createDataRow(newItem));
        });
        parent.appendChild(addRowButton);
    }
    createHeader(key) {
        const header = document.createElement('th');
        header.textContent = normalizeText(key);
        return header;
    }
    createDataRow(t) {
        const dataRow = document.createElement('tr');
        const indexedT = t;
        this.keys.forEach(key => dataRow.appendChild(this.createTableCell(key, indexedT)));
        const delCell = document.createElement('td');
        const delButton = document.createElement('button');
        const dataFinals = this.dataFinals;
        delButton.textContent = 'X';
        delButton.addEventListener('click', () => {
            dataRow.remove();
            dataFinals.splice(dataFinals.findIndex(d => d[this.primaryKey] === indexedT[this.primaryKey]), 1);
            console.log('deleted');
        });
        delCell.appendChild(delButton);
        dataRow.appendChild(delCell);
        return dataRow;
    }
    createTableCell(key, indexedT) {
        const tableCell = document.createElement('td');
        const cellValue = indexedT[key];
        const cellType = typeof this.sampleItem[key];
        if (key === this.primaryKey) {
            tableCell.textContent = cellValue;
        }
        else if (this.selOptions[key]) {
            const cellEditor = document.createElement('select');
            const selOptions = this.selOptions;
            const setSelOptions = this.setSelOptions;
            let prevOptions;
            setSelOptions(prevOptions, selOptions[key](indexedT), cellEditor, indexedT, key);
            cellEditor.addEventListener('click', () => {
                setSelOptions(prevOptions, selOptions[key](indexedT), cellEditor, indexedT, key);
            });
            cellEditor.addEventListener('change', () => indexedT[key] = Number(cellEditor.selectedOptions[0].value));
            tableCell.appendChild(cellEditor);
        }
        else {
            const cellEditor = document.createElement('input');
            switch (cellType) {
                case 'string':
                    cellEditor.type = 'text';
                    cellEditor.value = cellValue;
                    cellEditor.addEventListener('change', () => indexedT[key] = cellEditor.value);
                    break;
                case 'number':
                    cellEditor.type = 'number';
                    cellEditor.value = cellValue.toString();
                    cellEditor.addEventListener('change', () => indexedT[key] = Number(cellEditor.value));
                    break;
                case 'boolean':
                    cellEditor.type = 'checkbox';
                    cellEditor.checked = cellValue;
                    cellEditor.addEventListener('change', () => indexedT[key] = cellEditor.checked);
                    break;
                case 'bigint':
                case 'function':
                case 'object':
                case "symbol":
                case "undefined":
                    cellEditor.type = 'text';
                    cellEditor.value = cellValue.toString();
                    cellEditor.disabled = true;
            }
            tableCell.appendChild(cellEditor);
        }
        return tableCell;
    }
    //getDefault(val: any) {
    //    switch (typeof val) {
    //        case 'string':
    //            return '';
    //        case 'number':
    //        case 'bigint':
    //            return 0;
    //        case 'boolean':
    //            return false;
    //        case 'function':
    //        case 'object':
    //        case "symbol":
    //        case "undefined":
    //            return '';
    //    }
    //}
    getChanges() {
        const changes = [];
        for (const data of this.dataFinals) {
            const indexedData = data;
            if (indexedData[this.primaryKey] < 0) {
                changes.push({
                    changeType: ChangeType.Add,
                    item: data
                });
            }
            else {
                const orig = this.dataOriginals.find(d => d[this.primaryKey] === indexedData[this.primaryKey]);
                if (orig && this.dataChanged(orig, indexedData)) {
                    changes.push({
                        changeType: ChangeType.Edit,
                        item: data
                    });
                }
            }
        }
        for (const data of this.dataOriginals) {
            const indexedData = data;
            if (!this.dataFinals.some(d => d[this.primaryKey] === indexedData[this.primaryKey])) {
                changes.push({
                    changeType: ChangeType.Delete,
                    item: data
                });
            }
        }
        return changes;
    }
    dataChanged(orig, indexedData) {
        const indexedOrig = orig;
        return this.keys.some(key => indexedOrig[key] !== indexedData[key]);
    }
    setSelOptions(prevOptions, opts, select, indexedT, key) {
        if (prevOptions != opts) {
            prevOptions = opts;
            select.replaceChildren();
            const blankOpt = document.createElement('option');
            blankOpt.selected = true;
            blankOpt.value = "-1";
            select.add(blankOpt);
            opts.forEach(opt => {
                const option = document.createElement('option');
                option.text = opt.name;
                option.value = opt.id.toString();
                option.selected = indexedT[key] == opt.id;
                if (option.selected) {
                    blankOpt.selected = false;
                }
                select.add(option);
            });
            if (blankOpt.selected) {
                indexedT[key] = -1;
            }
        }
    }
}
class DelayedAction {
    #action;
    #delayLength;
    #triggerStartTime;
    constructor(delayInMs, action) {
        this.#delayLength = delayInMs;
        this.#action = action;
        this.#triggerStartTime = 0;
    }
    async start() {
        this.#triggerStartTime = Date.now();
        await delay(this.#delayLength);
        const now = Date.now();
        if (now - this.#triggerStartTime >= this.#delayLength * 0.95) {
            this.#triggerStartTime = now;
            this.#action();
        }
    }
}
class LinkedList {
    head;
    tail;
    length;
    comparator;
    constructor(comparator) {
        this.head = null;
        this.tail = null;
        this.length = 0;
        this.comparator = comparator;
    }
    appendData(data) {
        let node = new ListNode(this, data);
        if (this.tail) {
            node.prev = this.tail;
            this.tail.next = node;
        }
        else {
            this.head = node;
        }
        this.tail = node;
        this.length++;
    }
    prependData(data) {
        let node = new ListNode(this, data);
        if (this.head) {
            node.next = this.head;
            this.head.prev = node;
        }
        else {
            this.tail = node;
        }
        this.head = node;
        this.length++;
    }
    insertSorted(data) {
        let newNode = new ListNode(this, data);
        if (this.head) {
            let curr = this.head;
            while (curr && !this.comparator(curr.data, data)) {
                curr = curr.next;
            }
            if (curr === this.head) {
                this.head.prev = newNode;
                newNode.next = this.head;
                this.head = newNode;
            }
            else if (curr) {
                newNode.prev = curr.prev;
                newNode.prev.next = newNode;
                newNode.next = curr;
                curr.prev = newNode;
            }
            else {
                this.tail.next = newNode;
                newNode.prev = this.tail;
                this.tail = newNode;
            }
        }
        else {
            this.head = newNode;
            this.tail = newNode;
        }
        this.length++;
    }
    forEach(func) {
        let curr = this.head;
        let next;
        while (curr) {
            next = curr.next;
            func(curr.data);
            curr = next;
        }
    }
    forEachFromEnd(func) {
        let curr = this.tail;
        let prev;
        while (curr) {
            prev = curr.prev;
            func(curr.data);
            curr = prev;
        }
    }
    select(func) {
        let results = [];
        let curr = this.head;
        while (curr) {
            results.push(func(curr.data));
            curr = curr.next;
        }
        return results;
    }
    first(func) {
        let curr = this.head;
        while (curr && !func(curr.data)) {
            curr = curr.next;
        }
        return curr?.data;
    }
    firstFromEnd(func) {
        let curr = this.tail;
        while (curr && !func(curr.data)) {
            curr = curr.prev;
        }
        return curr?.data;
    }
    sort() {
        if (this.head && this.head.next) {
            let curr = this.head.next;
            let prev = this.head;
            let next = curr.next;
            while (curr) {
                if (this.comparator(prev.data, curr.data)) {
                    if (curr.next) {
                        curr.next.prev = curr.prev;
                    }
                    curr.prev.next = curr.next;
                    prev = prev.prev;
                    while (prev && this.comparator(prev.data, curr.data)) {
                        prev = prev.prev;
                    }
                    curr.prev = prev;
                    if (prev) {
                        curr.next = prev.next;
                        curr.next.prev = curr;
                        curr.prev.next = curr;
                    }
                    else {
                        this.head.prev = curr;
                        curr.next = this.head;
                        this.head = curr;
                    }
                }
                curr = next;
                if (curr) {
                    prev = curr.prev;
                    next = curr.next;
                }
            }
        }
    }
    remove(data) {
        let curr = this.head;
        while (curr && curr.data !== data) {
            curr = curr.next;
        }
        if (curr === this.head) {
            this.head = curr.next;
        }
        if (curr === this.tail) {
            this.tail = curr.prev;
        }
        curr?.prev && (curr.prev.next = curr.next);
        curr?.next && (curr.next.prev = curr.prev);
    }
}
class ListNode {
    data;
    prev;
    next;
    list;
    constructor(list, data) {
        this.list = list;
        data.lNode = this;
        this.data = data;
        this.next = null;
        this.prev = null;
    }
    sortSelf() {
        if (this.prev && this.list.comparator(this.prev.data, this.data)) {
            if (this.list.tail === this) {
                this.list.tail = this.prev;
            }
            if (this.next) {
                this.next.prev = this.prev;
            }
            this.prev.next = this.next;
            let prev = this.prev.prev;
            while (prev && this.list.comparator(prev.data, this.data)) {
                prev = prev.prev;
            }
            this.prev = prev;
            if (prev) {
                this.next = prev.next;
                this.next.prev = this;
                this.prev.next = this;
            }
            else {
                this.list.head.prev = this;
                this.next = this.list.head;
                this.list.head = this;
            }
        }
        else if (this.next && !this.list.comparator(this.next.data, this.data)) {
            if (this.list.head === this) {
                this.list.head = this.next;
            }
            if (this.prev) {
                this.prev.next = this.next;
            }
            this.next.prev = this.prev;
            let next = this.next.next;
            while (next && !this.list.comparator(next.data, this.data)) {
                next = next.next;
            }
            this.next = next;
            if (next) {
                this.prev = next.prev;
                this.prev.next = this;
                this.next.prev = this;
            }
            else {
                this.list.tail.next = this;
                this.prev = this.list.tail;
                this.list.tail = this;
            }
        }
    }
    /*
    sortSelf() {
        if(this.prev && this.list.comparator(this.prev.data, this.data)) {
            if(this.list.tail === this) {
                this.list.tail = this.prev;
            }
            if(this.next) {
                this.next.prev = this.prev;
            }
            this.prev.next = this.next;
            let curr = this.prev;
            let prev = curr.prev;
            while(curr && )
        } else if (this.next && !this.list.comparator(this.prev.data, this.data))
    }
    */
    removeSelf() {
        if (this === this.list.head) {
            this.list.head = this.next;
        }
        if (this === this.list.tail) {
            this.list.tail = this.prev;
        }
        this.prev && (this.prev.next = this.next);
        this.next && (this.next.prev = this.prev);
    }
}
var ChangeType;
(function (ChangeType) {
    ChangeType[ChangeType["Edit"] = 0] = "Edit";
    ChangeType[ChangeType["Add"] = 1] = "Add";
    ChangeType[ChangeType["Delete"] = 2] = "Delete";
})(ChangeType || (ChangeType = {}));
// randomInt between num1 (inclusive) and num2 (exclusive)
function randomInt(num1, num2) {
    return Math.floor(num1 + Math.random() * (num2 - num1));
}
// formats a number to a specific string representation
function formatNum(num) {
    return "" + parseFloat(num.toFixed(2));
}
async function delay(delay) {
    return new Promise(res => {
        setTimeout(() => res(), delay);
    });
}
function keys(obj) {
    return obj ? Object.keys(obj) : [];
}
function capitalize(str) {
    return str[0].toUpperCase() + str.slice(1);
}
function normalizeText(str) {
    return capitalize(str).replaceAll(/([a-z])([A-Z])/g, "$1 $2");
}
function plural(str) {
    const last = str.length - 1;
    switch (str.at(last)) {
        case 'y':
            return str.slice(0, last) + 'ies';
        case 'x':
            return str + 'es';
        case 's':
            return str;
        default:
            return str + 's';
    }
}
function groupBy(arr, groupFn) {
    const ret = {};
    arr.forEach(t => {
        const key = groupFn(t);
        if (ret[key]) {
            ret[key].push(t);
        }
        else {
            ret[key] = [t];
        }
    });
    return ret;
}
class ApiResponse {
    #r;
    #ev;
    #responseJson;
    constructor(r, ev) {
        this.#r = r;
        this.#ev = ev;
    }
    get status() {
        return this.#r.status;
    }
    get data() {
        if (!this.responseJson.data && this.responseJson.error) {
            throw new Error(this.responseJson.error);
        }
        return this.responseJson.data;
    }
    get error() {
        return this.responseJson.error;
    }
    get responseText() {
        return this.#r.responseText;
    }
    get responseJson() {
        return this.#responseJson ??= JSON.parse(this.#r.responseText);
    }
}
class DataCache {
    #data;
    #retrievalTask;
    #retriever;
    constructor(retriever) {
        this.#retriever = retriever;
        this.#retrievalTask = null;
    }
    get data() {
        if (this.#data) {
            return Promise.resolve(this.#data);
        }
        else if (!this.#retrievalTask) {
            this.#retrievalTask = this.#retriever().then(res => {
                this.#data = res;
                this.#retrievalTask = null;
                return res;
            });
        }
        return this.#retrievalTask;
    }
}
class AddEditItemModTool {
    static modTable;
    static renderParent;
    static async init(renderParent) {
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
class AddEditItemSlotTool {
    static slotTable;
    static renderParent;
    static tableDiv;
    static itemSelect;
    static itemCache = new DataCache(() => ApiRequest.get('/api/Item/Items'));
    static items;
    static slotTypeCache = new DataCache(() => ApiRequest.get('/api/Item/SlotTypes'));
    static itemModCache = new DataCache(() => ApiRequest.get('/api/ItemMod/ItemMods'));
    static async init(renderParent) {
        this.initCaches();
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
        this.items = await this.itemCache.data;
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
        const itemOpts = this.items.map(item => ({
            id: item.itemId,
            name: item.itemName
        }));
        const data = await Promise.all([
            this.slotTypeCache.data,
            this.itemModCache.data,
        ]);
        const slotTypeOpts = data[0].map(slotType => ({
            id: slotType.slotTypeId,
            name: slotType.slotTypeName
        }));
        const groupedMods = groupBy(data[1], i => i.slotTypeId.toString());
        const itemModOpts = {};
        keys(groupedMods).forEach(key => {
            itemModOpts[Number(key)] = groupedMods[key].map(mod => ({
                id: mod.itemModId,
                name: mod.itemModName
            }));
        });
        this.itemSelect.addEventListener('change', async () => {
            AddEditItemSlotTool.tableDiv.hidden = false;
            const selected = AddEditItemSlotTool.itemSelect.selectedOptions[0];
            const itemId = Number(selected.value);
            const itemSlots = await ApiRequest.get('/api/Item/SlotsForItem', { itemId: itemId });
            this.slotTable = new TableDataEditor(itemSlots, AddEditItemSlotTool.tableDiv, "itemSlotId", {
                "itemId": (i) => itemOpts,
                "slotTypeId": (i) => slotTypeOpts,
                "guaranteedId": (i) => itemModOpts[i.slotTypeId]
            }, {
                itemSlotId: 0,
                itemId: itemId,
                guaranteedId: 0,
                slotTypeId: 1,
                probability: 1.00
            });
        });
        this.renderParent = renderParent;
        const submitButton = document.createElement('button');
        submitButton.textContent = 'Save';
        submitButton.style.marginTop = '1em';
        submitButton.addEventListener('click', () => {
            AddEditItemSlotTool.submit();
        });
        renderParent.appendChild(submitButton);
    }
    static async submit() {
        const changes = this.slotTable.getChanges();
        if (changes.length > 0) {
            await new ApiRequest('/api/AdminTools/AddEditItemSlots').post(changes);
            AddEditItemSlotTool.init(this.renderParent);
        }
    }
    static initCaches() {
        this.itemCache.data;
        this.slotTypeCache.data;
        this.itemModCache.data;
    }
}
class AddEditItemTool {
    static itemTable;
    static renderParent;
    static async init(renderParent) {
        const itemCategoryOpts = (await ApiRequest.get('/api/ItemCategory/ItemCategories')).map(cat => ({
            id: cat.itemCategoryId,
            name: cat.categoryName
        }));
        const getItemCategories = (i) => {
            return itemCategoryOpts;
        };
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
class AddEditTagTool {
    static tagsTable;
    static renderParent;
    static async init(renderParent) {
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
class SetItemModTagsTool {
    static sel;
    static checkDiv;
    static items;
    static tags;
    static selectedItemId;
    static async init(renderParent) {
        const reqs = Promise.all([
            ApiRequest.get('/api/ItemMod/ItemMods'),
            ApiRequest.get('/api/Tags/Tags')
        ]);
        this.initHtml(renderParent);
        const res = await reqs;
        this.items = res[0];
        this.tags = res[1];
        this.items.splice(1).forEach(item => {
            const selOption = document.createElement('option');
            selOption.value = item.itemModId.toString();
            selOption.text = item.itemModName;
            this.sel.appendChild(selOption);
        });
        this.tags.forEach(tag => {
            const checkBox = document.createElement('input');
            checkBox.type = 'checkbox';
            checkBox.id = 'checkbox' + tag.tagId;
            checkBox.value = tag.tagId.toString();
            const checkLabel = document.createElement('label');
            checkLabel.htmlFor = 'checkbox' + tag.tagId;
            checkLabel.textContent = tag.tagName;
            this.checkDiv.appendChild(checkBox);
            this.checkDiv.appendChild(checkLabel);
            this.checkDiv.appendChild(document.createElement('br'));
        });
        this.sel.addEventListener('change', (ev) => {
            const selected = SetItemModTagsTool.sel.selectedOptions[0];
            SetItemModTagsTool.setCheckBoxes(selected.value);
        });
    }
    static initHtml(renderParent) {
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
    static async setCheckBoxes(itemId) {
        this.selectedItemId = Number(itemId);
        const selectedTags = ApiRequest.get('/api/Tags/TagsForItemMod', { itemModId: this.selectedItemId });
        for (const child of this.checkDiv.getElementsByTagName('input')) {
            child.checked = false;
        }
        (await selectedTags).forEach(tag => {
            const checkB = document.getElementById('checkbox' + tag.tagId);
            checkB.checked = true;
        });
    }
    static async submit() {
        const selectedTagIds = [];
        for (const child of this.checkDiv.getElementsByTagName('input')) {
            if (child.checked) {
                selectedTagIds.push(Number(child.value));
            }
        }
        new ApiRequest('/api/AdminTools/SetTagsForItemMod').post({ id: this.selectedItemId, tagIds: selectedTagIds });
    }
}
class SetItemTagsTool {
    static sel;
    static checkDiv;
    static items;
    static tags;
    static selectedItemId;
    static async init(renderParent) {
        const reqs = Promise.all([
            ApiRequest.get('/api/Item/Items'),
            ApiRequest.get('/api/Tags/Tags')
        ]);
        this.initHtml(renderParent);
        const res = await reqs;
        this.items = res[0];
        this.tags = res[1];
        this.items.splice(1).forEach(item => {
            const selOption = document.createElement('option');
            selOption.value = item.itemId.toString();
            selOption.text = item.itemName;
            this.sel.appendChild(selOption);
        });
        this.tags.forEach(tag => {
            const checkBox = document.createElement('input');
            checkBox.type = 'checkbox';
            checkBox.id = 'checkbox' + tag.tagId;
            checkBox.value = tag.tagId.toString();
            const checkLabel = document.createElement('label');
            checkLabel.htmlFor = 'checkbox' + tag.tagId;
            checkLabel.textContent = tag.tagName;
            this.checkDiv.appendChild(checkBox);
            this.checkDiv.appendChild(checkLabel);
            this.checkDiv.appendChild(document.createElement('br'));
        });
        this.sel.addEventListener('change', (ev) => {
            const selected = SetItemTagsTool.sel.selectedOptions[0];
            SetItemTagsTool.setCheckBoxes(selected.value);
        });
    }
    static initHtml(renderParent) {
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
            SetItemTagsTool.submit();
        });
        renderParent.appendChild(submitButton);
    }
    static async setCheckBoxes(itemId) {
        this.selectedItemId = Number(itemId);
        const selectedTags = ApiRequest.get('/api/Tags/TagsForItem', { itemId: this.selectedItemId });
        for (const child of this.checkDiv.getElementsByTagName('input')) {
            child.checked = false;
        }
        (await selectedTags).forEach(tag => {
            const checkB = document.getElementById('checkbox' + tag.tagId);
            checkB.checked = true;
        });
    }
    static async submit() {
        const selectedTagIds = [];
        for (const child of this.checkDiv.getElementsByTagName('input')) {
            if (child.checked) {
                selectedTagIds.push(Number(child.value));
            }
        }
        new ApiRequest('/api/AdminTools/SetTagsForItem').post({ id: this.selectedItemId, tagIds: selectedTagIds });
    }
}
