import type { ELogType, IBattlerAttribute } from "../"

export interface ILoginCredentials {
	username: string;
	password: string;
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
};

export interface ILogPreference {
	id: ELogType;
	enabled: boolean;
};