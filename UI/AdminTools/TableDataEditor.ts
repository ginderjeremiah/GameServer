import { TableDataRow } from "./TableDataRow";
import { TableDataOptions } from "./TableDataOptions";
import { keys, normalizeText } from "../Shared/GlobalFunctions";
import { EChangeType, IChange } from "../Shared/Api/Types";
import { Dict } from "../Shared/GlobalInterfaces";

export class TableDataEditor<T extends {}> {
    dataRows: TableDataRow<T>[];
    table: HTMLTableElement;
    sampleItem: T;
    newItemIndex = -1;

    constructor(data: T[], parent: HTMLElement, options: TableDataOptions<T>) {
        parent.replaceChildren();
        if (data.length > 0 && !data[0]) {
            data = data.splice(1); 
        }
        this.sampleItem = options.sampleItem ?? data[0];
        this.table = document.createElement('table');
        this.table.appendChild(this.createHeaderRow(this.sampleItem, options.primaryKey, options.hiddenColumns));

        this.dataRows = data.map((d, i) => new TableDataRow(i, d, options));
        this.dataRows.forEach(r => this.table.appendChild(r.createDataRow()));
        
        parent.appendChild(this.table);

        const addRowButton = document.createElement('button');
        addRowButton.textContent = 'Add Row';
        addRowButton.style.marginTop = '1em';
        addRowButton.addEventListener('click', () => {
            const newItem: Dict<any> = Object.assign({}, this.sampleItem);
            if (options.primaryKey) {
                newItem[options.primaryKey] = this.newItemIndex;
            }
            const newRow = new TableDataRow(this.newItemIndex, newItem as T, options);
            this.dataRows.push(newRow);
            this.table.appendChild(newRow.createDataRow());
            this.newItemIndex--;
        });
        parent.appendChild(addRowButton);
    }

    createHeaderRow(sampleItem: T, primaryKey?: string, hiddenColumns?: string[]) {
        const headerRow = document.createElement('tr');
        if (!primaryKey) {
            const indexHeader = document.createElement("th");
            indexHeader.textContent = "Index";
            headerRow.appendChild(indexHeader);
        }
        keys(sampleItem).forEach(key => {
            if (!hiddenColumns || hiddenColumns.indexOf(key) === -1) {
                const header = document.createElement('th');
                if (key.endsWith("Id") && key !== primaryKey) {
                    key = key.slice(0, -2);
                }
                var displayText = normalizeText(key);
                header.textContent = displayText;
                headerRow.appendChild(header);
            }
        });
        const delHeader = document.createElement('th');
        delHeader.textContent = 'Delete';
        headerRow.appendChild(delHeader);
        return headerRow;
    }

    getChanges() {
        const changes: IChange<T>[] = [];
        for (const row of this.dataRows) {
            if (row.isDeleted) {
                changes.push({
                    changeType: EChangeType.Delete,
                    item: row.data
                });
            } else if (row.index < 0) {
                changes.push({
                    changeType: EChangeType.Add,
                    item: row.data
                });
            } else {
                if (row.dataChanged()) {
                    changes.push({
                        changeType: EChangeType.Edit,
                        item: row.data
                    });
                }
            }
        }
        return changes;
    }
}
