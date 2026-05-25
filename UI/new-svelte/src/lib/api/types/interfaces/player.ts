import type { ELogType, IBattlerAttribute, IInventoryData } from "../"

export interface ILoginCredentials {
	username: string;
	password: string;
};

export interface ILogPreference {
	id: ELogType;
	enabled: boolean;
};

export interface IPlayerData {
	name: string;
	level: number;
	exp: number;
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	currentZone: number;
	statPointsGained: number;
	statPointsUsed: number;
	logPreferences: ILogPreference[];
	inventoryData: IInventoryData;
};