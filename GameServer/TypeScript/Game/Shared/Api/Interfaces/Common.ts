import { EChangeType } from "../Types"

export interface IChange<T1> {
	item: T1;
	changeType: EChangeType;
}