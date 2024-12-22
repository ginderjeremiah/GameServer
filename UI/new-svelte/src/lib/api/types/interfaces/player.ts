import type { ELogSetting, IBattlerAttribute, IInventoryData } from "../"

export interface ILoginCredentials {
	username: string;
	password: string;
};

export interface ILogPreference {
	id: ELogSetting;
	enabled: boolean;
};

export interface IPlayerData {
	userName: string;
	name: string;
	level: number;
	exp: number;
	attributes: IBattlerAttribute[];
	selectedSkills: number[];
	currentZone: number;
	statPointsGained: number;
	statPointsUsed: number;
	inventoryData: IInventoryData;
	logPreferences: ILogPreference[];
};