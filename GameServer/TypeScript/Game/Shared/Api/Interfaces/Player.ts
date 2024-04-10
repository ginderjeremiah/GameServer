interface IInventoryData {
	inventory: IInventoryItem[];
	equipped: IInventoryItem[];
}

interface ILoginCredentials {
	username: string;
	password: string;
}

interface ILoginData {
	currentZone: number;
	playerData: IPlayerData;
}

interface ILogPreference {
	name: string;
	enabled: boolean;
}

interface IPlayerData {
	userName: string;
	name: string;
	level: number;
	exp: number;
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	statPointsGained: number;
	statPointsUsed: number;
}