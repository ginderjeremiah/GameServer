import { ELogType, IBattlerAttribute, IInventoryData, ILogPreference, IPlayerData, IUnlockedSkill } from '$lib/api';
import { EXP_PER_LEVEL, STAT_POINTS_PER_LEVEL } from '$lib/api/types/game-constants';
import { formatNum, statify } from '$lib/common';
import { logMessage } from '../log';

export class PlayerManager implements IPlayerData {
	public name = '';
	public level = 0;
	public exp = 0;
	public currentZone = 0;
	public statPointsGained = 0;
	public statPointsUsed = 0;
	public attributes: IBattlerAttribute[] = [];
	public unlockedSkills: IUnlockedSkill[] = [];
	public logPreferences: ILogPreference[] = [];
	public inventoryData: IInventoryData = {
		unlockedItems: [],
		unlockedMods: []
	};

	/**
	 * The equipped skill ids in loadout order, derived from the unlocked set. The wire payload
	 * carries only the richer {@link unlockedSkills}; the ordered equipped list is its single
	 * source of truth (parallel to deriving equipped items from inventoryData.unlockedItems).
	 */
	public get selectedSkills(): number[] {
		return this.unlockedSkills
			.filter((skill) => skill.selected)
			.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
			.map((skill) => skill.skillId);
	}

	public initialize(data: IPlayerData) {
		this.name = data.name;
		this.level = data.level;
		this.exp = data.exp;
		this.currentZone = data.currentZone;
		this.statPointsGained = data.statPointsGained;
		this.statPointsUsed = data.statPointsUsed;
		this.attributes = data.attributes;
		this.unlockedSkills = data.unlockedSkills;
		this.logPreferences = data.logPreferences;
		this.inventoryData = data.inventoryData;
	}

	/**
	 * Called when the player unlocks a new skill from a challenge reward. The skill is added to the
	 * unlocked set unselected — earning a skill does not equip it (the loadout is chosen separately),
	 * mirroring the backend `Player.UnlockSkill`. The array is reassigned so reactive consumers (the
	 * skills screen) re-derive.
	 */
	public addUnlockedSkill(skillId: number) {
		if (this.unlockedSkills.some((skill) => skill.skillId === skillId)) {
			return;
		}
		this.unlockedSkills = [...this.unlockedSkills, { skillId, selected: false }];
		logMessage(ELogType.ItemFound, 'New skill unlocked!');
	}

	public grantExp(exp: number) {
		logMessage(ELogType.Exp, `Earned ${formatNum(exp)} exp.`);
		this.exp += exp;
		while (this.exp >= this.level * EXP_PER_LEVEL) {
			this.levelUp();
		}
	}

	public levelUp() {
		this.exp -= this.level * EXP_PER_LEVEL;
		this.level++;
		this.statPointsGained += STAT_POINTS_PER_LEVEL;
		logMessage(ELogType.LevelUp, 'Congratulations, you leveled up!');
		logMessage(ELogType.LevelUp, `You are now level ${this.level}.`);
	}
}

// The app-wide singleton lives with its class (rather than in engine.ts) so that
// `log.ts` — which PlayerManager depends on — can read it back without dragging
// the eager engine wiring into the import graph. See docs/frontend.md.
export const playerManager = statify(new PlayerManager());
