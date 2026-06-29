import { goto } from '$app/navigation';
import { resolve } from '$app/paths';
import { BattleEngine } from './battle/battle-engine';
import {
	statify,
	type Action,
	resolveUnlockReward,
	challengeCompletedMessage,
	proficiencyXpMessage,
	proficiencyLevelMessage,
	proficiencyMilestoneMessage,
	proficiencyOpenedMessage,
	creatableRecipeIds,
	recipeAvailableMessage
} from '$lib/common';
import { RenderEngine } from './render-engine';
import { LogicalEngine } from './logical-engine';
import { BackgroundThrottleMonitor } from './background-throttle-notice';
import { InventoryManager } from './player/inventory-manager';
import { playerManager } from './player/player-manager';
import { logMessage } from './log';
import { EnemyManager } from './battle/enemy-manager';
import {
	staticData,
	statistics,
	playerChallenges,
	playerProficiencies,
	acknowledgeModal,
	resetLogs,
	toastSuccess,
	navigation
} from '$stores';
import {
	apiSocket,
	ELogType,
	type IApiSocketResponse,
	type IProficiencyXpResultModel,
	type IProficiencyOpenedModel
} from '$lib/api';

export const inventoryManager = statify(new InventoryManager());
export const enemyManager = statify(new EnemyManager());

export const logicEngine = statify(new LogicalEngine());
export const renderEngine = statify(new RenderEngine());
export const battleEngine = statify(new BattleEngine());

// No reactive state to expose — it only fires a toast — so it isn't statified.
const backgroundThrottleMonitor = new BackgroundThrottleMonitor();

export const SESSION_REPLACED_TITLE = 'Session Replaced';
export const SESSION_REPLACED_BODY =
	'Another session has started elsewhere. You have been disconnected. You will be taken to the login screen.';

let socketReplacedUnhook: Action | undefined;
let challengeCompletedUnhook: Action | undefined;
let proficiencyXpGainedUnhook: Action | undefined;
let serverCommandFailedUnhook: Action | undefined;

export const startGame = () => {
	if (staticData.loaded) {
		inventoryManager.initialize();
		// Fire-and-forget: the per-zone ZonesCleared values back the fight screen's
		// boss "Cleared" seal; a failed load just leaves the seal hidden.
		void statistics.load();
		// Challenge completion gates zone navigation (a zone unlocks once its gating challenge is
		// completed); a failed load just leaves zones gated as-is until the next load.
		void playerChallenges.load();
		// Proficiency levels feed the live battler's per-level attribute bonuses (mirroring the backend
		// battle snapshot). Loaded before the battle engine starts so the first enemy — fetched over the
		// same serialized socket after this command — is fought against a battler with the bonus applied.
		void playerProficiencies.load();
		startLogicEngine();
		startRenderEngine();
		startBattleEngine();
		socketReplacedUnhook = apiSocket.listenCommand('SocketReplaced', handleSocketReplaced, true);
		challengeCompletedUnhook = apiSocket.listenCommand('ChallengeCompleted', handleChallengeCompleted, true);
		proficiencyXpGainedUnhook = apiSocket.listenCommand('ProficiencyXpGained', handleProficiencyXpGained, true);
		serverCommandFailedUnhook = apiSocket.listenCommand('ServerCommandFailed', handleServerCommandFailed, true);
	}
};

/**
 * Reacts to a ServerCommandFailed notice: the server dead-lettered a server-pushed command that threw and
 * is telling us to re-sync rather than silently diverge from the authoritative state. Two pushes leave
 * divergent client state: ChallengeCompleted (its completion gates zone navigation) and ProficiencyXpGained
 * (its levels feed the live battler's attribute bonuses), so a failed one force-reloads the matching
 * authoritative progress; any reward/skill it carried still surfaces on the next natural load of the
 * inventory/skills stores.
 */
export const handleServerCommandFailed = (response: IApiSocketResponse<'ServerCommandFailed'>) => {
	const failedCommand = response.data?.commandName;
	console.warn(`A server-pushed command failed on the server and was dead-lettered: ${failedCommand ?? 'unknown'}.`);
	if (failedCommand === 'ChallengeCompleted') {
		void playerChallenges.load(true);
	} else if (failedCommand === 'ProficiencyXpGained') {
		void playerProficiencies.load(true);
	}
};

/**
 * Applies a completed-challenge push from the server: marks the challenge complete (so completion-gated
 * UI like zone navigation and the reward reveal update without a refetch), unlocks each reward it carries
 * so the player can use it immediately — equip the item/mod — instead of only after a page refresh, and
 * surfaces a success toast announcing the completion and what it unlocked.
 */
export const handleChallengeCompleted = (response: IApiSocketResponse<'ChallengeCompleted'>) => {
	const data = response.data;
	if (!data) {
		return;
	}

	playerChallenges.markCompleted(data.challengeId);

	if (data.rewardItemId != null) {
		inventoryManager.addUnlockedItem({
			itemId: data.rewardItemId,
			equipped: false,
			favorite: false,
			appliedMods: []
		});
	}
	if (data.rewardItemModId != null) {
		inventoryManager.addUnlockedMod(data.rewardItemModId);
	}

	notifyChallengeCompleted(data.challengeId);
};

/**
 * Surfaces a success toast naming the completed challenge and the reward it unlocked, so the player is
 * told even when the challenge finished on another screen mid-idle. A cross-screen toast is used over the
 * arena-anchored Zone-Cleared overlay since challenges complete during the continuously-running battle
 * regardless of the active screen. The "View" action opens the Challenges screen to inspect the reward.
 */
const notifyChallengeCompleted = (challengeId: number) => {
	const challenge = staticData.challenges?.[challengeId];
	if (!challenge) {
		return;
	}
	const reward = resolveUnlockReward(challenge, {
		items: staticData.items,
		itemMods: staticData.itemMods
	});
	toastSuccess(challengeCompletedMessage(challenge.name, reward), {
		action: { label: 'View', onClick: () => navigation.requestScreen('challenges') }
	});
};

/**
 * Applies a proficiency-XP push from a won battle (spike #982 area H): surfaces the per-proficiency XP,
 * level-ups, milestones, and newly-opened proficiencies, makes any milestone reward skill usable
 * immediately (mirroring the challenge-reward unlock), then updates the proficiency store so the tree and
 * the live battler's bonuses reflect the gain without a refetch. Prior levels are read off the store
 * before it is updated, since the push carries only the new level — the level-up is detected by comparison.
 */
export const handleProficiencyXpGained = (response: IApiSocketResponse<'ProficiencyXpGained'>) => {
	const data = response.data;
	if (!data) {
		return;
	}

	// Snapshot what was creatable before applying the gain, so the recipes that newly become creatable from
	// this push — a milestone-granted input skill, or a level-up crossing a recipe's condition gate — can be
	// toasted below once the owned skills and proficiency levels reflect it.
	const creatableBefore = currentCreatableRecipeIds();

	for (const result of data.proficiencies) {
		notifyProficiencyResult(result, playerProficiencies.levelOf(result.proficiencyId));
		for (const skillId of result.grantedSkillIds) {
			playerManager.addUnlockedSkill(skillId);
		}
	}
	for (const opened of data.opened) {
		notifyProficiencyOpened(opened);
	}

	playerProficiencies.applyXpGained(data);

	notifyNewlyCreatableRecipes(creatableBefore);
};

/** The recipe ids the player can synthesize right now, derived from the live owned (unlocked) skills and
 *  proficiency levels against the delivered recipes — the same client-side availability the Synthesis
 *  screen derives. Snapshotted before/after a progress push to detect a recipe that newly became creatable. */
const currentCreatableRecipeIds = (): Set<number> =>
	creatableRecipeIds(
		staticData.skillRecipes ?? [],
		new Set(playerManager.unlockedSkills.map((skill) => skill.skillId)),
		new Map(playerProficiencies.all.map((proficiency) => [proficiency.proficiencyId, proficiency.level]))
	);

/**
 * Toasts each recipe that became creatable as a result of the just-applied proficiency gain, comparing the
 * current creatable set against the pre-push snapshot. Mirrors the milestone/challenge feedback — a success
 * toast with a "View" action into the Synthesis screen — so the player learns a new recipe opened up even
 * while idling on another screen. Synthesizing is player-driven and stays un-toasted (spike #1125 area F);
 * this only fires for an availability change driven by background progress. Unknown reference ids are skipped.
 */
const notifyNewlyCreatableRecipes = (creatableBefore: ReadonlySet<number>) => {
	const recipes = staticData.skillRecipes;
	if (!recipes) {
		return;
	}
	const creatableNow = currentCreatableRecipeIds();
	for (const recipe of recipes) {
		if (!creatableNow.has(recipe.id) || creatableBefore.has(recipe.id)) {
			continue;
		}
		const result = staticData.skills?.[recipe.resultSkillId];
		if (!result) {
			continue;
		}
		toastSuccess(recipeAvailableMessage(result.name), {
			action: { label: 'View', onClick: () => navigation.requestScreen('synthesis') }
		});
	}
};

/**
 * Surfaces one proficiency's outcome. Routine XP goes to the combat log only (it lands every won battle,
 * like the player's own XP); a level-up adds a log line, and crossing a milestone both logs and toasts —
 * naming the granted skill, resolved from the proficiency's authored level rewards. A plain level-up also
 * toasts, but only when no milestone was crossed at the same time, so the richer milestone toast isn't
 * duplicated. Unknown reference ids are skipped (the store is still updated by the caller).
 */
const notifyProficiencyResult = (result: IProficiencyXpResultModel, previousLevel: number) => {
	const proficiency = staticData.proficiencies?.[result.proficiencyId];
	if (!proficiency) {
		return;
	}
	const name = proficiency.name;

	const xp = Math.round(result.xpGained);
	if (xp > 0) {
		logMessage(ELogType.Proficiency, proficiencyXpMessage(name, xp));
	}

	const leveledUp = result.newLevel > previousLevel;
	if (leveledUp) {
		logMessage(ELogType.Proficiency, proficiencyLevelMessage(name, result.newLevel));
	}

	for (const milestoneLevel of result.milestonesCrossed) {
		const skillId = proficiency.levelRewards.find((reward) => reward.level === milestoneLevel)?.rewardSkillId;
		const skillName = skillId != null ? staticData.skills?.[skillId]?.name : undefined;
		const message = proficiencyMilestoneMessage(name, milestoneLevel, skillName);
		logMessage(ELogType.Proficiency, message);
		toastSuccess(message);
	}

	if (leveledUp && result.milestonesCrossed.length === 0) {
		toastSuccess(proficiencyLevelMessage(name, result.newLevel));
	}
};

/** Surfaces a newly-opened proficiency (a maxed tier's next tier, or a newly-satisfied gateway).
 *  Notification-only — opening grants no skill. Both a combat-log line and a toast, since an unlock is a
 *  notable, infrequent event. Unknown reference ids are skipped. */
const notifyProficiencyOpened = (opened: IProficiencyOpenedModel) => {
	const proficiency = staticData.proficiencies?.[opened.proficiencyId];
	if (!proficiency) {
		return;
	}
	const message = proficiencyOpenedMessage(proficiency.name);
	logMessage(ELogType.Proficiency, message);
	toastSuccess(message);
};

// Use goto() not location.href — a reload re-runs the boot gate and bounces an authenticated client back into the game.
export const handleSocketReplaced = async () => {
	stopGame();
	await acknowledgeModal({
		title: SESSION_REPLACED_TITLE,
		body: SESSION_REPLACED_BODY,
		confirmLabel: 'Go to Login'
	});
	void goto(resolve('/'));
};

/**
 * Stops the live game loops (logical, render, battle) and the background-throttle monitor. Shared by the
 * full {@link stopGame} teardown and the game route's own unmount cleanup, so navigating away from the
 * game stops the loops without tearing down the socket/stores a session-replacement must clear. Every
 * stop is idempotent, so this is safe to call even when the loops never started (e.g. the player left
 * during the welcome-back gate, before {@link startGame} ran).
 */
export const stopEngines = () => {
	logicEngine.stop();
	backgroundThrottleMonitor.stop();
	renderEngine.stop();
	enemyManager.stop();
	battleEngine.stop();
};

const stopGame = () => {
	stopEngines();
	statistics.reset();
	playerChallenges.reset();
	playerProficiencies.reset();
	resetLogs();
	socketReplacedUnhook?.();
	challengeCompletedUnhook?.();
	proficiencyXpGainedUnhook?.();
	serverCommandFailedUnhook?.();
	// SocketReplaced routes back to login client-side (no reload), so the socket singleton survives. Tear
	// it down explicitly, otherwise the keepalive ping would silently reconnect and fight the session that
	// just took over.
	apiSocket.disconnect();
};

const startLogicEngine = () => {
	logicEngine.start();
	backgroundThrottleMonitor.start();
};

const startRenderEngine = () => {
	renderEngine.start();
};

const startBattleEngine = () => {
	enemyManager.start();
	battleEngine.start();
};
