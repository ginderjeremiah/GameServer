import type { IItemDrop } from "../"

export interface IZone {
	id: number;
	name: string;
	description: string;
	order: number;
	levelMin: number;
	levelMax: number;
	zoneDrops: IItemDrop[];
};

export interface IZoneEnemy {
	enemyId: number;
	weight: number;
};

export interface ISetZoneEnemiesData {
	zoneId: number;
	zoneEnemies: IZoneEnemy[];
};