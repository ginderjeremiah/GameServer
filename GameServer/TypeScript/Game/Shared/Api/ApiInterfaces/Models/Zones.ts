interface IZone {
	zoneId: number;
	zoneName: string;
	zoneDesc: string;
	zoneOrder: number;
	levelMin: number;
	levelMax: number;
	zoneDrops: IItemDrop[];
}