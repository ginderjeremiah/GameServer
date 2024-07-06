interface IDefeatEnemyResponse {
	cooldown: number;
	rewards?: IDefeatRewards;
}

interface IDefeatRewards {
	expReward: number;
	drops: IInventoryItem[];
}

interface IEnemy {
	enemyId: number;
	name: string;
	drops: IItemDrop[];
	attributeDistribution: IAttributeDistribution[];
	skillPool: number[];
}

interface IEnemyInstance {
	id: number;
	level: number;
	attributes: IBattlerAttribute[];
	seed: number;
	selectedSkills: number[];
}

interface INewEnemyModel {
	cooldown?: number;
	enemyInstance?: IEnemyInstance;
}

interface INewEnemyRequest {
	newZoneId?: number;
}