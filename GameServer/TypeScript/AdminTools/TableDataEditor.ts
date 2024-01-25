class TableDataEditor<T extends {}> {
    dataOriginals: T[];
    dataFinals: T[];
    table: HTMLTableElement;
    sampleItem: T;
    keys: string[];
    selOptions: { [key: string]: (i: T) => SelOption[] };
    primaryKey: string;
    newItemIndex = -1;

    constructor(data: T[], parent: HTMLElement, primaryKey: string, selOptions: { [key: string]: (i: T) => SelOption[]}, sampleItem?: T) {
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
            const newItem: Dict<any> = {};
            this.keys.forEach(key => {
                if (key == this.primaryKey) {
                    newItem[this.primaryKey] = this.newItemIndex--;
                } else {
                    newItem[key] = (this.sampleItem as Dict<any>)[key];
                }
            });
            (this.dataFinals as Dict<any>[]).push(newItem);
            this.table.appendChild(this.createDataRow(newItem as T));
        });
        parent.appendChild(addRowButton);
    }

    createHeader(key: string) {
        const header = document.createElement('th');
        header.textContent = normalizeText(key);
        return header;
    }

    createDataRow(t: T) {
        const dataRow = document.createElement('tr');
        const indexedT: Dict<any> = t;

        this.keys.forEach(key => dataRow.appendChild(this.createTableCell(key, indexedT)));

        const delCell = document.createElement('td');
        const delButton = document.createElement('button');
        const dataFinals = this.dataFinals;
        delButton.textContent = 'X';
        delButton.addEventListener('click', () => {
            dataRow.remove();
            dataFinals.splice(dataFinals.findIndex(d => (d as Dict<any>)[this.primaryKey] === indexedT[this.primaryKey]), 1);
            console.log('deleted');
        });
        delCell.appendChild(delButton);
        dataRow.appendChild(delCell);

        return dataRow;
    }

    createTableCell(key: string, indexedT: Dict<any>) {
        const tableCell = document.createElement('td');
        const cellValue = indexedT[key];
        const cellType = typeof (this.sampleItem as Dict<any>)[key];

        if (key === this.primaryKey) {
            tableCell.textContent = cellValue;
        } else if (this.selOptions[key]) {
            const cellEditor = document.createElement('select');
            const selOptions = this.selOptions;
            const setSelOptions = this.setSelOptions;
            let prevOptions: SelOption[] | undefined;
            setSelOptions(prevOptions, selOptions[key](indexedT as T), cellEditor, indexedT, key);
            cellEditor.addEventListener('click', () => {
                setSelOptions(prevOptions, selOptions[key](indexedT as T), cellEditor, indexedT, key);
            });
            cellEditor.addEventListener('change', () => indexedT[key] = Number(cellEditor.selectedOptions[0].value));
            tableCell.appendChild(cellEditor);
        } else {
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
        const changes: Change<T>[] = [];
        for (const data of this.dataFinals) {
            const indexedData = data as Dict<any>;
            if (indexedData[this.primaryKey] < 0) {
                changes.push({
                    changeType: ChangeType.Add,
                    item: data
                });
            } else {
                const orig = this.dataOriginals.find(d => (d as Dict<any>)[this.primaryKey] === indexedData[this.primaryKey]);
                if (orig && this.dataChanged(orig, indexedData)) {
                    changes.push({
                        changeType: ChangeType.Edit,
                        item: data
                    });
                }
            }
        }
        for (const data of this.dataOriginals) {
            const indexedData = data as Dict<any>
            if (!this.dataFinals.some(d => (d as Dict<any>)[this.primaryKey] === indexedData[this.primaryKey])) {
                changes.push({
                    changeType: ChangeType.Delete,
                    item: data
                });
            }
        }
        return changes;
    }

    dataChanged(orig: T, indexedData: Dict<any>) {
        const indexedOrig = orig as Dict<any>;
        return this.keys.some(key => indexedOrig[key] !== indexedData[key]);
    }

    setSelOptions(prevOptions: SelOption[] | undefined, opts: SelOption[], select: HTMLSelectElement, indexedT: Dict<any>, key: string) {
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
