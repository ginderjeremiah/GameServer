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
	private logPreferenceList: ILogPreference[] = [];
	public inventoryData: IInventoryData = {
		unlockedItems: [],
		unlockedMods: []
	};

	/** O(1) `enabled`-by-type lookup, rebuilt whenever {@link logPreferences} is assigned, so the
	 *  per-tick combat-log writer (`log.ts`) never linear-scans the preference list. `#`-private so
	 *  `statify` leaves it alone — it is read imperatively, never inside a reactive derivation. */
	#enabledByType = new Map<ELogType, boolean>();

	/**
	 * Memoised equipped skill ids in loadout order, derived from {@link unlockedSkills}. The wire
	 * payload carries only the richer unlocked set; this ordered list is its single source of truth
	 * (parallel to deriving equipped items from inventoryData.unlockedItems). Rebuilt only on the
	 * mutations that change the loadout ({@link initialize}/{@link setSelectedSkills}/
	 * {@link addUnlockedSkill}), so the per-spawn battle reset and reactive consumers read a stable
	 * array instead of re-running filter+sort+map on every access (#811).
	 */
	private selectedSkillsCache: number[] = [];

	public get selectedSkills(): number[] {
		return this.selectedSkillsCache;
	}

	/** Recomputes the memoised {@link selectedSkills} from the current unlocked set's selected/order. */
	private refreshSelectedSkills() {
		this.selectedSkillsCache = this.unlockedSkills
			.filter((skill) => skill.selected)
			.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
			.map((skill) => skill.skillId);
	}

	/**
	 * Replaces the equipped loadout with `orderedIds` in priority order, updating each unlocked skill's
	 * selected flag + slot order and refreshing the memoised {@link selectedSkills}. The single loadout
	 * mutation path (mirroring how the inventory manager centralizes equipment changes), so battles and
	 * other screens read the new equipped set/order without a reload.
	 */
	public setSelectedSkills(orderedIds: number[]) {
		for (const unlockedSkill of this.unlockedSkills) {
			const order = orderedIds.indexOf(unlockedSkill.skillId);
			unlockedSkill.selected = order >= 0;
			// An unequipped skill has no loadout slot, so clear its order rather than pinning it to 0
			// (which would conflate "unequipped" with "first slot").
			unlockedSkill.order = order >= 0 ? order : undefined;
		}
		this.refreshSelectedSkills();
	}

	/** The player's combat-log preferences. Assigning rebuilds the by-type enabled lookup. */
	public get logPreferences(): ILogPreference[] {
		return this.logPreferenceList;
	}
	public set logPreferences(preferences: ILogPreference[]) {
		this.logPreferenceList = preferences;
		this.#enabledByType = new Map(preferences.map((pref): [ELogType, boolean] => [pref.id, pref.enabled]));
	}

	/** Whether a log type is enabled, defaulting an unknown type to enabled (matching the combat-log
	 *  writer's historical `?? true` fallback). O(1) via the prebuilt by-type map. */
	public logTypeEnabled(logType: ELogType): boolean {
		return this.#enabledByType.get(logType) ?? true;
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
		this.refreshSelectedSkills();
	}

	/**
	 * Called when the player unlocks a new skill from a challenge reward. The skill is added to the
	 * unlocked set unselected — earning a skill does not equip it (the loadout is chosen separately),
	 * mirroring the backend `Player.UnlockSkill`.
	 */
	public addUnlockedSkill(skillId: number) {
		if (this.unlockedSkills.some((skill) => skill.skillId === skillId)) {
			return;
		}
		this.unlockedSkills.push({ skillId, selected: false });
		// A newly-unlocked skill is unselected, so the equipped loadout is unchanged — but refresh the
		// memo so it stays consistent with the unlocked set rather than relying on that invariant.
		this.refreshSelectedSkills();
		logMessage(ELogType.ItemFound, 'New skill unlocked!');
	}

	/** Exp required to advance the current level (`Level * EXP_PER_LEVEL`, mirroring the backend).
	 *  The level is clamped to ≥ 1 so the pre-`initialize` `level = 0` default can't produce a
	 *  zero threshold that would let `grantExp` over-level. A real level (≥ 1) is unaffected.
	 *  Public so the fight-screen XP bar derives its fill from the same threshold the level-up
	 *  logic uses, rather than re-deriving `level * EXP_PER_LEVEL` in the view. */
	public get nextLevelThreshold(): number {
		return Math.max(1, this.level) * EXP_PER_LEVEL;
	}

	public grantExp(exp: number) {
		logMessage(ELogType.Exp, `Earned ${formatNum(exp)} exp.`);
		this.exp += exp;
		let threshold = this.nextLevelThreshold;
		// A non-positive threshold (a hypothetical EXP_PER_LEVEL constant regression) would spin this
		// loop forever; guard it so a single bad constant can't lock up the game.
		while (threshold > 0 && this.exp >= threshold) {
			this.levelUp();
			threshold = this.nextLevelThreshold;
		}
	}

	public levelUp() {
		this.exp -= this.nextLevelThreshold;
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
