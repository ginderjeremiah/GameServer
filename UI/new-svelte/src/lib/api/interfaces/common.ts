import { EChangeType } from "../"

export interface IChange<T> {
	item: T;
	changeType: EChangeType;
}