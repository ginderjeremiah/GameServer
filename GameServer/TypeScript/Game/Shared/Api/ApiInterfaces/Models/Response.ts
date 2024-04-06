interface IDefeatEnemy {
	cooldown: number;
	rewards: IDefeatRewards;
}

interface INewEnemy {
	cooldown: number;
	enemyInstance: IEnemyInstance;
}

interface ILoginData {
	currentZone: number;
	playerData: IPlayerData;
}

interface IPlayerData {
	userName: string;
	playerName: string;
	level: number;
	exp: number;
	attributes: IPlayerAttribute[];
	selectedSkills: number[];
	statPointsGained: number;
	statPointsUsed: number;
}

interface IInventoryData {
	inventory: IInventoryItem[];
	equipped: IInventoryItem[];
}

interface IDefeatRewards {
	expReward: number;
	drops: IInventoryItem[];
}