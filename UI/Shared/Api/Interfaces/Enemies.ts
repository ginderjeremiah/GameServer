import {
	IAttributeDistribution,
	IBattlerAttribute,
	IInventoryItem,
	IItemDrop
} from "../Types"

export interface IEnemy {
	enemyId: number;
	name: string;
	drops: IItemDrop[];
	attributeDistribution: IAttributeDistribution[];
	skillPool: number[];
}

export interface IDefeatEnemyResponse {
	cooldown: number;
	rewards?: IDefeatRewards;
}

export interface IEnemyInstance {
	id: number;
	level: number;
	attributes: IBattlerAttribute[];
	seed: number;
	selectedSkills: number[];
}

export interface INewEnemyModel {
	cooldown?: number;
	enemyInstance?: IEnemyInstance;
}

export interface INewEnemyRequest {
	newZoneId?: number;
}

export interface IDefeatRewards {
	expReward: number;
	drops: IInventoryItem[];
}