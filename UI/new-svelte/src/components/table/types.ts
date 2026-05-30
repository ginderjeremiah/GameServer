import { SelectOptions } from '$components/Select.svelte';
import type { Snippet } from 'svelte';

export interface EditorOptions<T extends {}> {
	data: T[];
	primaryKey?: keyof T;
	hiddenColumns?: (keyof T)[];
	disabledColumns?: (keyof T)[];
	selectOptions?: Partial<{ [key in keyof T]: (t: T) => SelectOptions }>;
	sampleItem?: T;
	title?: string;
	/** Called when the user clicks Save Changes in the footer save bar. */
	onSave?: () => void | Promise<void>;
	/** Optional control (e.g. an enemy/zone picker) rendered above the table. */
	gate?: Snippet;
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

/**
 * Type-aware equality so a typed "8" compares equal to the original number 8,
 * etc. Used to derive whether a cell/row differs from its saved value.
 */
export function valuesEqual(a: unknown, b: unknown): boolean {
	if (typeof a === 'number' || typeof b === 'number') {
		const na = a === '' || a == null ? null : Number(a);
		const nb = b === '' || b == null ? null : Number(b);
		return na === nb;
	}
	if (typeof a === 'boolean' || typeof b === 'boolean') {
		return !!a === !!b;
	}
	return String(a ?? '') === String(b ?? '');
}
