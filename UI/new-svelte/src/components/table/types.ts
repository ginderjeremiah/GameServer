import { SelectOptions } from '$components/Select.svelte';

export interface EditorOptions<T extends {}> {
	data: T[];
	primaryKey?: keyof T;
	hiddenColumns?: (keyof T)[];
	disabledColumns?: (keyof T)[];
	selectOptions?: Partial<{ [key in keyof T]: (t: T) => SelectOptions }>;
	sampleItem?: T;
}

export interface RowData<T extends {}> {
	originalData: T;
	data: T;
	index: number;
	state: RowState;
}

export interface ColumnData<T> {
	key: keyof T;
	name: string;
	disabled?: boolean;
}

export enum RowState {
	Unmodified,
	Modified,
	Added,
	Deleted,
	AddedDeleted
}
