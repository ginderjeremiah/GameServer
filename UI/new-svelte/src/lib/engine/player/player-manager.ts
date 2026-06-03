import { ELogType, IBattlerAttribute, IInventoryData, ILogPreference, IPlayerData } from '$lib/api';
import { formatNum, statify } from '$lib/common';
import { logMessage } from '../log';

const expPerLevel = 100;
const statPointsPerLevel = 6;

export class PlayerManager implements IPlayerData {
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
		unlockedItems: [],
		unlockedMods: []
	};

	public initialize(data: IPlayerData) {
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
		logMessage(ELogType.Exp, `Earned ${formatNum(exp)} exp.`);
		this.exp += exp;
		if (this.exp >= this.level * expPerLevel) {
			this.levelUp();
		}
	}

	public levelUp() {
		this.exp -= this.level * expPerLevel;
		this.level++;
		this.statPointsGained += statPointsPerLevel;
		logMessage(ELogType.LevelUp, 'Congratulations, you leveled up!');
		logMessage(ELogType.LevelUp, `You are now level ${this.level}.`);
	}
}

// The app-wide singleton lives with its class (rather than in engine.ts) so that
// `log.ts` — which PlayerManager depends on — can read it back without dragging
// the eager engine wiring into the import graph. See docs/frontend.md.
export const playerManager = statify(new PlayerManager());
