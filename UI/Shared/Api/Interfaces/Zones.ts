import { IItemDrop } from "../Types"

export interface IZone {
	id: number;
	name: string;
	description: string;
	order: number;
	levelMin: number;
	levelMax: number;
	zoneDrops: IItemDrop[];
}