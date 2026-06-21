import { apiSocket, ELogType, IEnemyInstance } from '$lib/api';
import { Action, createHook, delay, isZoneUnlocked, navigableZones, nextZoneByOrder } from '$lib/common';
import { staticData, statistics, playerChallenges } from '$stores';
import { battleEngine, BattleStage, onBattleStageChanged, playerManager } from '../';
import { logMessage } from '../log';

const newEnemyLoadedHook = createHook<[IEnemyInstance]>();
const notifyNewEnemyLoaded = newEnemyLoadedHook.notify;
export const onNewEnemyLoaded = newEnemyLoadedHook.onNotified;

/**
 * Backoff before re-requesting an enemy when the server returns no enemy and no explicit cooldown
 * — i.e. the request failed (e.g. a transient socket/server error, so `data` is absent). Without it,
 * a persistent failure would spin `getNewEnemy` into a tight request loop.
 */
const NEW_ENEMY_RETRY_DELAY_MS = 1000;

/**
 * How long the Zone-Cleared overlay lingers after a dedicated-boss victory before the boss loop
 * either re-challenges (auto-fight) or hands back to the idle farm loop. Drives the pacing of the
 * "victory moment" the fight screen renders.
 */
const BOSS_VICTORY_OVERLAY_MS = 2600;

/**
 * The fight screen's two mutually-exclusive loops. `idle` farms the zone's random enemies (the
 * dedicated boss is "available" to challenge); `boss` runs a dedicated-boss fight (the idle loop is
 * paused). Per the boss design they never run simultaneously.
 */
export type FightMode = 'idle' | 'boss';

export class EnemyManager {
	public currentEnemy: IEnemyInstance | undefined;
	public started = false;

	/** Which loop is active. `boss` ⇒ the player is engaged in a dedicated-boss fight. */
	public mode: FightMode = 'idle';
	/** Whether a boss victory should immediately re-challenge the boss (farming). */
	public autoFight = false;
	/** Set briefly after a boss victory to drive the fight screen's Zone-Cleared overlay. */
	public bossOutcome: 'victory' | undefined;
	/** Whether the most recent boss victory unlocked the next zone (the gating challenge flipped from
	 *  incomplete to complete). Drives the Zone-Cleared overlay's "Next zone unlocked" line. */
	public bossUnlockedNextZone = false;

	private battleStageUnhook?: Action;
	/** Guards the challenge/retreat transitions so a resolving stage change for the battle being
	 *  swapped out is not mistaken for an outcome of the new one. Stays set across a supersession (a
	 *  press that takes over an in-flight transition) — only the transition that ends up current clears it. */
	private transitioning = false;
	/** Bumped whenever a new transition supersedes an in-flight one. A running transition captures this at
	 *  its start and, after each await, abandons (without applying its result) once it no longer matches — so
	 *  a superseded challenge/retreat can't clobber the transition that took over, and the superseded one's
	 *  finally won't release the `transitioning` guard the new transition still holds. */
	private transitionGeneration = 0;
	/** Re-entrancy guard for getNewEnemy: overlapping stage handlers must not both spawn-and-notify an
	 *  enemy (a double-spawn the backend replay would flag as cheating). */
	private fetchingEnemy = false;
	/** Bumped whenever the active enemy-fetch loop is superseded (a stop, or a transition taking over).
	 *  The running getNewEnemy captures this value and exits once it no longer matches, so a stale loop
	 *  under a sustained outage can't keep spinning — nor later clobber the new fight with an enemy
	 *  nobody is waiting for. */
	private fetchGeneration = 0;
	/** The generation the in-flight fetch loop captured at its start. Lets a fresh getNewEnemy tell a
	 *  genuine re-entrant duplicate (same generation — the running loop will still spawn, so drop) apart
	 *  from a superseded loop (older generation — it will abandon without spawning, so wait it out then
	 *  fetch fresh rather than be dropped by the re-entrancy guard). */
	private fetchingGeneration = 0;
	/** The in-flight fetch loop, so a superseding caller (e.g. a failing boss-challenge falling back to
	 *  the idle loop) can await its teardown before fetching afresh — keeping a single fetch in flight. */
	private inFlightFetch?: Promise<void>;
	/** Resolver that short-circuits an in-flight retry backoff so a stop / superseding transition need
	 *  not wait out the full delay before the loop re-checks whether it should still be running. */
	private cancelBackoff?: () => void;

	public start() {
		if (!this.started) {
			this.started = true;
			this.battleStageUnhook = onBattleStageChanged((stage) => this.watchBattleStage(stage));
			this.watchBattleStage(battleEngine.stage);
		}
	}

	public stop() {
		if (this.started) {
			this.started = false;
			this.battleStageUnhook?.();
			// Cut short an in-flight retry backoff so a getNewEnemy parked under a sustained outage exits
			// at once on the cleared `started` flag rather than after the full backoff window.
			this.interruptFetch();
			// Teardown (screen unmount / session end), not a user intent change, so don't sync the persisted
			// loop mode — clobbering it to idle here would lose a disconnecting boss-farmer's mode.
			this.returnToIdle(false);
		}
	}

	public async getNewEnemy(): Promise<void> {
		const generation = this.fetchGeneration;
		if (this.fetchingEnemy) {
			// A fetch is already running. If it captured this same generation it's a genuine re-entrant
			// duplicate — overlapping stage handlers (e.g. an idle victory racing an Idle/Defeated change)
			// each request-and-notify a spawn, and a double-spawn has anti-cheat consequences (the backend
			// replays what the client reports). Drop it; the running fetch still spawns one.
			if (this.fetchingGeneration === generation) {
				return;
			}
			// Otherwise this call's generation is newer, so the running fetch has been superseded (a
			// transition bumped the generation) and will abandon without spawning. Wait for it to tear
			// down, then fall through and fetch fresh so this fallback spawn isn't dropped by the guard.
			await this.inFlightFetch;
			// The wait can overlap a stop or a further supersession; bail if either landed meanwhile.
			if (!this.started || generation !== this.fetchGeneration) {
				return;
			}
		}
		const run = this.runFetchLoop(generation);
		this.inFlightFetch = run;
		await run;
	}

	private async runFetchLoop(generation: number): Promise<void> {
		this.fetchingEnemy = true;
		this.fetchingGeneration = generation;
		try {
			// Retry iteratively rather than via self-recursion: each attempt returns to a flat stack
			// (a sustained outage no longer grows the async chain without bound). The loop ends as soon
			// as `stop()` flips `started` or a transition supersedes this fetch (bumping the generation);
			// because the retry backoff is cancellable, either takes effect immediately rather than after
			// the full delay — so the controls don't feel frozen while a backoff is parked.
			while (this.started && generation === this.fetchGeneration) {
				const result = await apiSocket.sendSocketCommand('NewEnemy', {
					newZoneId: playerManager.currentZone
				});
				// The loop condition only gates the top of each iteration, so a stop / superseding
				// transition that lands while parked on the await above (not on the cancellable backoff)
				// would otherwise still apply this response. Re-check before applying so a successful
				// NewEnemy can't clobber the fight the supersession already moved on to.
				if (!this.started || generation !== this.fetchGeneration) {
					return;
				}
				if (result.data?.enemyInstance) {
					// The server may have relocated the player out of a now-unplayable zone (retired, or
					// emptied of spawnable enemies); adopt the authoritative zone so the UI and later
					// requests follow the move instead of re-requesting the stale one.
					if (result.data.zoneId != null && result.data.zoneId !== playerManager.currentZone) {
						playerManager.currentZone = result.data.zoneId;
					}
					this.currentEnemy = result.data.enemyInstance;
					notifyNewEnemyLoaded(this.currentEnemy);
					return;
				}

				// No enemy this time: either the zone is on cooldown (wait it out) or the request failed
				// (`data` is absent — note the optional chaining; an error response carries no data).
				// `||` (not `??`) so a no-enemy response with `cooldown: 0` still backs off rather than
				// tight-looping; an explicit positive cooldown is honored.
				if (result.error) {
					logMessage(ELogType.Debug, 'There was an error loading a new enemy: ' + result.error);
				}
				await this.backoff(result.data?.cooldown || NEW_ENEMY_RETRY_DELAY_MS);
			}
		} finally {
			this.fetchingEnemy = false;
		}
	}

	/**
	 * A cancellable retry backoff: resolves after `ms`, or early if {@link interruptFetch} fires, so a
	 * stop / superseding transition during a sustained outage need not wait out the full delay before
	 * the fetch loop re-checks its run condition. Still delegates the actual sleep to `delay` (so a
	 * positive cooldown is honored) rather than re-implementing the timer.
	 */
	private backoff(ms: number): Promise<void> {
		return new Promise<void>((resolve) => {
			let settled = false;
			const finish = () => {
				if (settled) {
					return;
				}
				settled = true;
				// Only clear the shared handle if it still points at this backoff — a later backoff may
				// have already replaced it (e.g. the real timer firing after an early cancel).
				if (this.cancelBackoff === finish) {
					this.cancelBackoff = undefined;
				}
				resolve();
			};
			this.cancelBackoff = finish;
			delay(ms).then(finish);
		});
	}

	/** Supersedes any in-flight enemy fetch: the running getNewEnemy loop ends at its next check (the
	 *  bumped generation no longer matches) and its retry backoff is cut short so the supersession takes
	 *  effect immediately rather than after the parked delay. */
	private interruptFetch() {
		this.fetchGeneration++;
		this.cancelBackoff?.();
	}

	/** Begins a control transition (challenge/retreat), superseding any in-flight one: it holds the
	 *  `transitioning` guard, bumps the transition generation (so a superseded transition abandons its
	 *  result), and cuts short any parked enemy-fetch backoff so the takeover is immediate. Returns the new
	 *  generation for the caller to re-check after each await. */
	private beginTransition(): number {
		this.transitioning = true;
		this.interruptFetch();
		return ++this.transitionGeneration;
	}

	/** Ends a control transition, releasing the guard — but only if this transition is still the current
	 *  one. A superseded transition leaving its finally must not clear the flag the transition that took
	 *  over still holds. */
	private endTransition(generation: number) {
		if (generation === this.transitionGeneration) {
			this.transitioning = false;
		}
	}

	/**
	 * Start a dedicated-boss fight against the current zone's boss. Switches into the boss loop
	 * (the backend abandons any in-progress idle fight) and engages. A bossless zone or a transient
	 * failure falls back to the idle loop.
	 */
	public async challengeBoss() {
		if (this.transitioning && this.mode === 'boss') {
			// A challenge is already in flight heading to the same destination (boss). Pressing again is
			// redundant — re-sending ChallengeBoss would make the backend abandon and re-spawn the boss — so
			// let the in-flight transition continue rather than start a second.
			return;
		}
		// Otherwise nothing is transitioning, or a retreat (heading to idle) is — supersede it: this press wins.
		const generation = this.beginTransition();
		this.mode = 'boss';
		this.bossOutcome = undefined;
		this.bossUnlockedNextZone = false;
		// Freeze the outgoing battle for the duration of the swap so it can't resolve mid-transition.
		battleEngine.pause();
		try {
			const result = await apiSocket.sendSocketCommand('ChallengeBoss', {
				zoneId: playerManager.currentZone
			});
			// A stop, or a later press superseding this challenge, landed while ChallengeBoss was in flight;
			// abandon so its result can't clobber the transition that took over (mirrors getNewEnemy's guard).
			if (!this.started || generation !== this.transitionGeneration) {
				return;
			}
			if (result.data?.enemyInstance) {
				this.currentEnemy = result.data.enemyInstance;
				notifyNewEnemyLoaded(this.currentEnemy);
			} else {
				if (result.error) {
					logMessage(ELogType.Debug, 'There was an error challenging the boss: ' + result.error);
				}
				this.returnToIdle();
				await this.getNewEnemy();
			}
		} finally {
			this.endTransition(generation);
		}
	}

	/** Retreat from an in-progress boss fight back to the normal idle farm loop. */
	public async retreatFromBoss() {
		if (this.transitioning) {
			if (this.mode === 'idle') {
				// A transition heading to the same destination (idle) is already in flight — a retreat, or a
				// failing challenge's idle fallback. Pressing again is redundant, so let it continue.
				return;
			}
			// Otherwise a challenge (heading to boss) is in flight — fall through to supersede it: retreat wins.
		} else if (this.mode !== 'boss') {
			// Not transitioning and not in a boss fight: nothing to retreat from.
			return;
		}
		const generation = this.beginTransition();
		this.returnToIdle();
		battleEngine.pause();
		try {
			// getNewEnemy self-guards via the fetch generation, so a press superseding this retreat is
			// abandoned there; endTransition then leaves the guard for whichever transition is current.
			await this.getNewEnemy();
		} finally {
			this.endTransition(generation);
		}
	}

	/** Toggle auto-fight: when on, a boss victory immediately re-challenges the boss. */
	public setAutoFight(on: boolean) {
		this.autoFight = on;
		// Persist the player's auto-fight intent (#1040's "mirror the live autoFight state" semantic). Note
		// auto-fight can be pre-armed from BossTrigger while the loop is still idle, so this persists boss
		// mode on intent rather than active engagement; whether to gate on mode==='boss' is tracked in #1067
		// (must be settled before the offline-sim consumer in #1041/#1042 reads the field).
		this.syncAutoChallengeBoss(on);
	}

	private returnToIdle(sync = true) {
		this.mode = 'idle';
		this.autoFight = false;
		this.bossOutcome = undefined;
		this.bossUnlockedNextZone = false;
		// Sync the persisted loop mode for genuine return-to-idle transitions (retreat, boss loss/draw,
		// single-victory handoff). Teardown passes sync=false — it must not reset the persisted intent.
		if (sync) {
			this.syncAutoChallengeBoss(false);
		}
	}

	/**
	 * Persists the active idle-loop mode to the backend so the offline-rewards sim can resume the correct
	 * loop at next login (idle vs. auto-challenge-boss). Mirrors the live auto-fight state: on ⇒ boss-farming
	 * the current zone, off ⇒ idle. Fire-and-forget — anti-cheat validation is the server's, and a transient
	 * failure only leaves the persisted mode briefly stale, corrected by the next sync or the backend's
	 * boss-loss/draw backstop.
	 */
	private syncAutoChallengeBoss(enabled: boolean) {
		void apiSocket.sendSocketCommand('SetAutoChallengeBoss', {
			enabled,
			zoneId: playerManager.currentZone
		});
	}

	/** Whether the boss loop is still the active, settled context — false once a stop / retreat / handoff
	 *  has transitioned us away. A boss-victory resolution re-checks this after each await so a transition
	 *  landing mid-resolution abandons it instead of clobbering the new state. */
	private get bossLoopActive(): boolean {
		return this.started && this.mode === 'boss' && !this.transitioning;
	}

	private async watchBattleStage(stage: BattleStage) {
		// While swapping the active battle, ignore the outgoing battle's resolving stage changes.
		if (this.transitioning) {
			return;
		}
		if (this.mode === 'boss') {
			await this.watchBossStage(stage);
		} else {
			await this.watchIdleStage(stage);
		}
	}

	private async watchIdleStage(stage: BattleStage) {
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			const cooldown = await this.claimVictory();
			if (cooldown > 0) {
				await battleEngine.startLoading(cooldown);
			}
		}

		// The awaited claim/cooldown above can overlap a boss challenge or retreat that hands the loop
		// off (and resolves the cooldown early via reset); if we've since left idle mode or a transition
		// is mid-flight, don't spawn an idle enemy over the new fight.
		if (this.transitioning || this.mode !== 'idle') {
			return;
		}

		// A draw (the 2-minute timeout) ends the fight with no rewards; the idle farm simply continues, the
		// same as a defeat. The unresolved battle is recorded as abandoned by the backend when the next
		// enemy starts (StartBattle re-simulates and resolves it).
		if (
			stage === BattleStage.Victorious ||
			stage === BattleStage.Defeated ||
			stage === BattleStage.Drawn ||
			stage === BattleStage.Idle
		) {
			await this.getNewEnemy();
		}
	}

	private async watchBossStage(stage: BattleStage) {
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			await this.resolveBossVictory();
		} else if (stage === BattleStage.Defeated && this.currentEnemy) {
			await this.resolveBossLoss();
		} else if (stage === BattleStage.Drawn && this.currentEnemy) {
			await this.resolveBossDraw();
		}
	}

	private async resolveBossVictory() {
		// Snapshot the boss's zone up front: it identifies the zone being cleared and the gate this clear
		// may unlock, and must stay fixed even if currentZone shifts (a zone-change / retreat) during the
		// awaits below — otherwise the clear and the "next zone unlocked" check could target the wrong zone.
		const clearedZoneId = playerManager.currentZone;

		await this.claimVictory();
		// A stop / retreat that landed during the victory claim has already transitioned us out of the boss
		// loop; abandon the resolution so we don't resurrect the cleared overlay over the new state.
		if (!this.bossLoopActive) {
			return;
		}

		// A dedicated-boss victory clears its zone; surface the "Cleared" seal immediately while the
		// authoritative per-zone statistic is reconciled on the next statistics load.
		statistics.markZoneCleared(clearedZoneId);

		// Did this clear unlock the next zone? Capture the next zone's locked state now (before the clear
		// is reconciled), then — only if it was actually locked — refresh challenge completion (the backend
		// completes the gating challenge during claimVictory) and check whether it flipped open. Skipping
		// the refresh when nothing could change keeps auto-fight re-farming from spamming the endpoint.
		const completed = (id: number) => playerChallenges.isChallengeCompleted(id);
		const nextZone = nextZoneByOrder(navigableZones(staticData.zones ?? []), clearedZoneId);
		const nextWasLocked = nextZone != null && !isZoneUnlocked(nextZone, completed);
		if (nextWasLocked) {
			await playerChallenges.load(true);
			// Re-guard after the reload for the same reason — a stop / retreat may have landed while the
			// challenge refresh was in flight.
			if (!this.bossLoopActive) {
				return;
			}
		}
		this.bossUnlockedNextZone = nextWasLocked && nextZone != null && isZoneUnlocked(nextZone, completed);

		this.bossOutcome = 'victory';

		await delay(BOSS_VICTORY_OVERLAY_MS);
		// A retreat / stop during the overlay window already transitioned us elsewhere.
		if (!this.bossLoopActive) {
			return;
		}
		this.bossOutcome = undefined;
		if (this.autoFight) {
			await this.challengeBoss();
		} else {
			this.returnToIdle();
			await this.getNewEnemy();
		}
	}

	private async resolveBossLoss() {
		// Record the loss explicitly (turning auto-fight off) and drop back to the boss-available
		// state — the normal idle farm loop — honoring the post-loss cooldown.
		const lostResponse = await apiSocket.sendSocketCommand('BattleLost');
		if (lostResponse.error) {
			logMessage(ELogType.Debug, 'There was an error recording the boss loss: ' + lostResponse.error);
		}
		this.returnToIdle();
		// An error response carries no `data` (e.g. a transient socket failure), so guard the
		// dereference the same way `getNewEnemy` does — otherwise a failed BattleLost would throw
		// before `getNewEnemy` runs and strand the player with no new enemy after a boss loss.
		const cooldown = lostResponse.data?.cooldown ?? 0;
		if (cooldown > 0) {
			await battleEngine.startLoading(cooldown);
		}
		await this.getNewEnemy();
	}

	/** Resolve a dedicated-boss fight that reached the 2-minute time limit. A timeout is a draw, not a
	 *  death, so it is never recorded as a loss — the player simply failed to clear the boss in time. Drop
	 *  back to the idle farm loop (boss available, auto-fight off) rather than re-spawning the boss; the
	 *  unresolved boss battle is recorded as abandoned by the backend when the next idle enemy starts. */
	private async resolveBossDraw() {
		this.returnToIdle();
		await this.getNewEnemy();
	}

	/**
	 * Reports the current enemy's defeat to the server and grants the earned exp. Shared by the idle
	 * and boss victory paths so the two cannot drift. Returns the post-victory cooldown (ms).
	 */
	private async claimVictory(): Promise<number> {
		// Resolve the enemy's name up front (guarding a missing/retired id), but only log the defeat
		// after a successful DefeatEnemy so a failed command can't show "X was defeated!" with no rewards.
		const enemyId = this.currentEnemy?.id;
		const enemyName = enemyId != null ? staticData.enemies?.[enemyId]?.name : undefined;
		const defeatResponse = await apiSocket.sendSocketCommand('DefeatEnemy', { timestamp: Date.now() });
		if (!defeatResponse.error && defeatResponse.data?.rewards) {
			if (enemyName) {
				logMessage(ELogType.EnemyDefeated, enemyName + ' was defeated!');
			}
			playerManager.grantExp(defeatResponse.data.rewards.expReward);
		} else {
			logMessage(ELogType.Debug, 'There was an error defeating the enemy: ' + defeatResponse.error);
		}
		// Guard `data` for a possible error response (absent `data`), now that this is the shared
		// victory path for both the idle and boss loops.
		return defeatResponse.data?.cooldown ?? 0;
	}
}
