interface TableDataOptions<T extends {}> {
    primaryKey?: string;
    hiddenColumns?: string[];
    disabledColumns?: string[];
    selOptions?: { [key: string]: (i: T) => SelOptions};
    sampleItem?: T
}
