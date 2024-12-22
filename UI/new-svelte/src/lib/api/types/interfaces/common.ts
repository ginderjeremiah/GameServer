import type { EChangeType } from "../"

export interface IChange<T> {
	item: T;
	changeType: EChangeType;
};

export interface IAddEditAttributesData {
	id: number;
	changes: IChange<IBattlerAttribute>[];
};