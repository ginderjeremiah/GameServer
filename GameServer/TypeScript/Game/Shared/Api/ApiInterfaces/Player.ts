interface ILoginData {
	currentZone: number;
	playerData: IPlayerData;
}

interface IPlayerData {
	userName: string;
	playerName: string;
	level: number;
	exp: number;
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	statPointsGained: number;
	statPointsUsed: number;
}

interface IInventoryData {
	inventory: IInventoryItem[];
	equipped: IInventoryItem[];
}

interface ILogPreference {
	name: string;
	enabled: boolean;
}