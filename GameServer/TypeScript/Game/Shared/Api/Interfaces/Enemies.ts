interface IDefeatEnemy {
	cooldown: number;
	rewards: IDefeatRewards;
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
	selectedSkills: number[];
}

interface IEnemyInstance {
	enemyId: number;
	level: number;
	attributes: IBattlerAttribute[];
	seed: number;
}

interface INewEnemy {
	cooldown: number;
	enemyInstance: IEnemyInstance;
}