import {
	ELogSetting,
	IBattlerAttribute,
	IInventoryData,
	ILogPreference,
	IPlayerData
} from '$lib/api';
import { formatNum } from '$lib/common';
import { logMessage } from '../log';

export class PlayerManager implements IPlayerData {
	public userName = '';
	public name = '';
	public level = 0;
	public exp = 0;
	public currentZone = 0;
	public statPointsGained = 0;
	public statPointsUsed = 0;
	public attributes: IBattlerAttribute[] = [];
	public selectedSkills: number[] = [];
	public logPreferences: ILogPreference[] = [];
	public inventoryData: IInventoryData = {
		inventory: [],
		equipped: []
	};

	public initialize(data: IPlayerData) {
		this.userName = data.userName;
		this.name = data.name;
		this.level = data.level;
		this.exp = data.exp;
		this.currentZone = data.currentZone;
		this.statPointsGained = data.statPointsGained;
		this.statPointsUsed = data.statPointsUsed;
		this.attributes = data.attributes;
		this.selectedSkills = data.selectedSkills;
		this.logPreferences = data.logPreferences;
		this.inventoryData = data.inventoryData;
	}

	public grantExp(exp: number) {
		logMessage(ELogSetting.Exp, `Earned ${formatNum(exp)} exp.`);
		this.exp += exp;
		if (this.exp >= this.level * 100) {
			this.levelUp();
		}
	}

	public levelUp() {
		this.exp -= this.level * 100;
		this.level++;
		this.statPointsGained += 6;
		logMessage(ELogSetting.LevelUp, 'Congratulations, you leveled up!');
		logMessage(ELogSetting.LevelUp, `You are now level ${this.level}.`);
	}
}
