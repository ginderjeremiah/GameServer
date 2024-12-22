import type {
	IAttributeDistribution,
	IBattlerAttribute,
	IInventoryItem,
	IItemDrop
} from "../"

export interface IEnemy {
	id: number;
	name: string;
	drops: IItemDrop[];
	attributeDistribution: IAttributeDistribution[];
	skillPool: number[];
};

export interface ISetEnemyAttributeDistributions {
	enemyId: number;
	attributeDistributions: IAttributeDistribution[];
};

export interface ISetEnemySkillsData {
	enemyId: number;
	skillIds: number[];
};

export interface IEnemyInstance {
	id: number;
	level: number;
	attributes: IBattlerAttribute[];
	seed: number;
	selectedSkills: number[];
};

export interface INewEnemyRequest {
	newZoneId?: number;
};

export interface IDefeatRewards {
	expReward: number;
	drops: IInventoryItem[];
};

export interface IDefeatEnemyResponse {
	cooldown: number;
	rewards?: IDefeatRewards;
};

export interface INewEnemyModel {
	cooldown?: number;
	enemyInstance?: IEnemyInstance;
};