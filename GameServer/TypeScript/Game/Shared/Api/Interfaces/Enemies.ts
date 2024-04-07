interface IDefeatEnemy {
	cooldown: number;
	rewards: IDefeatRewards;
}

interface IDefeatRewards {
	expReward: number;
	drops: IInventoryItem[];
}

interface IEnemy {
	enemyDrops: IItemDrop[];
	attributeDistribution: IAttributeDistribution[];
	enemyName: string;
	enemyId: number;
	selectedSkills: number[];
}

interface IEnemyInstance {
	enemyId: number;
	enemyLevel: number;
	attributes: IBattlerAttribute[];
	seed: number;
}

interface INewEnemy {
	cooldown: number;
	enemyInstance: IEnemyInstance;
}