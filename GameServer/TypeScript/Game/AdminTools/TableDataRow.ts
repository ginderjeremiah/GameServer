import { keys } from "../Shared/GlobalFunctions";
import { SelOptions, Dict } from "../Shared/GlobalInterfaces";
import { TableDataOptions } from "./TableDataOptions";

export class TableDataRow<T extends {}> {
    index: number;
    data: T;
    originalData: T;
    isDeleted: boolean;
    primaryKey?: string;
    hiddenColums?: string[];
    disabledColumns?: string[];
    selOptions: { [key: string]: (i: T) => SelOptions};

    constructor(index: number, data: T, options: TableDataOptions<T>) {
        if (options.primaryKey) {
            this.index = (data as Dict<any>)[options.primaryKey];
        } else {
            this.index = index;
        }
        this.data = data;
        this.originalData = JSON.parse(JSON.stringify(data));
        this.isDeleted = false;
        this.primaryKey = options.primaryKey;
        this.hiddenColums = options.hiddenColumns;
        this.disabledColumns = options.disabledColumns;
        this.selOptions = options.selOptions ?? {};
    }

    createDataRow() {
        const dataRow = document.createElement('tr');
        const indexedData: Dict<any> = this.data as Dict<any>;

        if (!this.primaryKey) {
            const indexCell = document.createElement("td");
            indexCell.textContent = this.index.toString();
            dataRow.appendChild(indexCell);
        }

        keys(indexedData).forEach(key => {
            if (!this.hiddenColums || this.hiddenColums.indexOf(key) === -1) {
                dataRow.appendChild(this.createTableCell(key))
            }
        });

        const delCell = document.createElement('td');
        const delButton = document.createElement('button');
        delButton.textContent = 'X';
        delButton.addEventListener('click', () => {
            dataRow.remove();
            this.isDeleted = true;
        });
        delCell.appendChild(delButton);
        dataRow.appendChild(delCell);

        return dataRow;
    }

    createTableCell(key: string) {
        const indexedT = this.data as Dict<any>;
        const tableCell = document.createElement('td');
        const cellValue = indexedT[key];
        const cellType = typeof indexedT[key];
        const disabled = (!!this.disabledColumns && this.disabledColumns.indexOf(key) !== -1);

        if (key === this.primaryKey) {
            tableCell.textContent = cellValue;
        } else if (this.selOptions[key]) {
            const cellEditor = document.createElement('select');
            const selOptions = this.selOptions;
            const setSelOptions = this.setSelOptions;
            let prevOptions: SelOptions | undefined;
            setSelOptions(prevOptions, selOptions[key](this.data), cellEditor, indexedT, key);
            cellEditor.addEventListener('click', () => {
                setSelOptions(prevOptions, selOptions[key](this.data), cellEditor, indexedT, key);
            });
            cellEditor.addEventListener('change', () => indexedT[key] = Number(cellEditor.selectedOptions[0].value));
            cellEditor.disabled = disabled;
            tableCell.appendChild(cellEditor);
        } else {
            const cellEditor = document.createElement('input');
            cellEditor.disabled = disabled;
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
                case 'object':
                case 'bigint':
                case 'function':
                case "symbol":
                case "undefined":
                    cellEditor.type = 'text';
                    cellEditor.value = cellValue?.toString();
                    cellEditor.disabled = true;
            }
            tableCell.appendChild(cellEditor);
        }

        return tableCell;
    }

    dataChanged() {
        const indexedOrig = this.originalData as Dict<any>;
        const indexedData = this.data as Dict<any>
        return keys(indexedOrig).some(key => indexedOrig[key] !== indexedData[key]);
    }

    setSelOptions(prevOptions: SelOptions | undefined, opts: SelOptions, select: HTMLSelectElement, indexedT: Dict<any>, key: string) {
        if (prevOptions != opts) {
            prevOptions = opts;
            select.replaceChildren();
            const blankOpt = document.createElement('option');
            if (opts.allowBlanks) {
                blankOpt.selected = true;
                blankOpt.value = "-1";
                select.add(blankOpt);
            }
            opts.options.forEach(opt => {
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