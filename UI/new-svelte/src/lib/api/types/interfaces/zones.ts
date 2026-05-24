export interface IZone {
	id: number;
	name: string;
	description: string;
	order: number;
	levelMin: number;
	levelMax: number;
};

export interface IZoneEnemy {
	enemyId: number;
	weight: number;
};

export interface ISetZoneEnemiesData {
	zoneId: number;
	zoneEnemies: IZoneEnemy[];
};