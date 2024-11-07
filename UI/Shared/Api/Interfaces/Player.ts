import { IBattlerAttribute, IInventoryData } from "../Types"

export interface ILoginData {
	currentZone: number;
	playerData: IPlayerData;
}

export interface ILoginCredentials {
	username: string;
	password: string;
}

export interface IPlayerData {
	userName: string;
	name: string;
	level: number;
	exp: number;
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	statPointsGained: number;
	statPointsUsed: number;
	inventoryData: IInventoryData;
	logPreferences: ILogPreference[];
}

export interface ILogPreference {
	name: string;
	enabled: boolean;
}