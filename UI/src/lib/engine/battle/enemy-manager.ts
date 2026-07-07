import { apiSocket, ELogType, IEnemyInstance } from '$lib/api';
import { Action, createHook, delay, isZoneUnlocked, navigableZones, nextZoneByOrder } from '$lib/common';
import { staticData, statistics, playerChallenges } from '$stores';
import {
	battleEngine,
	BattleStage,
	inventoryManager,
	onBattleStageChanged,
	playerManager,
	type PlayerBattleState
} from '../';
import { logMessage } from '../log';
import { refreshPlayer } from '../session';

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

/** The data a battle-end (DefeatEnemy / BattleLost) response carries: the post-battle cooldown plus the
 *  optional server-prefetched next idle battle bundled to hide its fetch latency under the cooldown. */
interface BattleEndResult {
	cooldown: number;
	nextEnemy?: IEnemyInstance;
	nextZoneId?: number;
}

/** A server-prefetched next idle battle, held across the post-battle cooldown so the loop can begin it
 *  without a separate NewEnemy round-trip. It is used only if still valid when the cooldown elapses — same
 *  zone and unchanged player battle-state ({@link EnemyManager.preparedStillValid}); otherwise the loop
 *  falls back to a fresh fetch. */
interface PreparedBattle {
	enemy: IEnemyInstance;
	/** The zone the server spawned the prefetched enemy in (authoritative after any relocation). */
	zoneId: number;
	/** The player's battle-relevant inputs when the battle was prepared, so a build change during the
	 *  cooldown can be detected and the now-divergent prefetch discarded (parity). */
	playerState: PlayerBattleState;
}

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
	/** The last `AutoChallengeBoss` value synced to the backend, so {@link syncAutoChallengeBoss} drops
	 *  redundant re-syncs (e.g. a returnToIdle when the persisted mode is already idle). `undefined` until
	 *  the first sync, so the first authoritative state is always sent. */
	private lastSyncedAutoChallengeBoss?: boolean;

	/**
	 * Starts the fight loop. `activeBattle` is the server-handed-back battle a `GetOfflineProgress` summary
	 * carried (#1595/#1596: a stale battle still genuinely in progress, or the away-window's trailing
	 * remainder) — presented directly rather than through the normal fetch, since the idle loop's first
	 * `NewEnemy` would otherwise report 0ms fought and abandon it with no outcome (#1597). Absent, the loop
	 * starts exactly as before: the initial stage (`Idle`) drives a fresh fetch.
	 *
	 * A handed-back battle's `isBossBattle` (the authoritative `PlayerState.IsBossBattle`) routes the mode
	 * accordingly (#1647), so a resumed boss fight is presented through the boss loop instead of always
	 * defaulting to idle.
	 */
	public start(activeBattle?: IEnemyInstance) {
		if (!this.started) {
			this.started = true;
			this.battleStageUnhook = onBattleStageChanged((stage) => this.watchBattleStage(stage));
			if (activeBattle) {
				this.mode = activeBattle.isBossBattle ? 'boss' : 'idle';
				this.currentEnemy = activeBattle;
				notifyNewEnemyLoaded(activeBattle);
			} else {
				this.watchBattleStage(battleEngine.stage);
			}
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

	/**
	 * Navigate the player to a zone (driven by the zone-nav arrows). Combat zones apply on the next spawn as
	 * before — the running fight finishes and the following NewEnemy carries the new zone. The no-combat Home
	 * sanctuary is special: entering it stops the live battle and halts the idle loop (the player rests, no
	 * enemies spawn), and leaving it resumes the loop in the destination zone. The backend never persists Home
	 * as the player's current zone (it refuses a battle zone-change into one), so offline rewards keep
	 * crediting their last real combat zone.
	 */
	public navigateToZone(zoneId: number) {
		const previousZone = playerManager.currentZone;
		if (zoneId === previousZone) {
			return;
		}
		const leavingHome = this.isHomeZone(previousZone);
		playerManager.currentZone = zoneId;
		if (this.isHomeZone(zoneId)) {
			this.enterHome();
		} else if (leavingHome) {
			// Resuming combat from the resting state — spawn the first enemy in the destination zone.
			void this.getNewEnemy();
		}
	}

	/**
	 * Park the player in the no-combat Home sanctuary: supersede any in-flight fetch, leave the boss loop, drop
	 * the current enemy, and stop the live battle. No NewEnemy is sent while at Home (the fight loop is halted);
	 * an in-progress backend battle is left to be resolved as an abandon by the next real battle when the player
	 * leaves Home (the standard zone-change/abandon machinery).
	 */
	private enterHome() {
		this.interruptFetch();
		this.returnToIdle();
		this.currentEnemy = undefined;
		battleEngine.rest();
	}

	/** Whether the given zone id is the no-combat Home sanctuary, per the reference data. */
	private isHomeZone(zoneId: number): boolean {
		return staticData.zones?.[zoneId]?.isHome === true;
	}

	/**
	 * Spawns the next idle enemy, notifying listeners once one is ready. Optionally takes a server-prefetched
	 * {@link PreparedBattle}: when still valid it is presented immediately with no `NewEnemy` round-trip (its
	 * latency was hidden under the post-battle cooldown), otherwise a fresh one is fetched. The present runs
	 * *inside* this method's re-entrancy/supersession guard (rather than from the stage handler directly), so
	 * a prefetched enemy can never double-spawn with a concurrent fetch nor clobber a fight a transition moved
	 * on to — the same guarantees the fetch path relies on.
	 */
	public async getNewEnemy(prepared?: PreparedBattle): Promise<void> {
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
		const run = this.runFetchLoop(generation, prepared);
		this.inFlightFetch = run;
		await run;
	}

	private async runFetchLoop(generation: number, prepared?: PreparedBattle): Promise<void> {
		this.fetchingEnemy = true;
		this.fetchingGeneration = generation;
		try {
			// The no-combat Home sanctuary never spawns enemies, so halt the loop rather than sending NewEnemy:
			// the backend refuses a battle zone-change into Home and would answer with a real-zone enemy, which
			// would resume fighting while the player is meant to be resting. Clearing the current enemy lets the
			// fight screen render the resting state.
			if (this.isHomeZone(playerManager.currentZone)) {
				this.currentEnemy = undefined;
				return;
			}
			// A still-valid server-prefetched battle is presented without a round-trip. Done here (inside the
			// fetchingEnemy/generation guard) rather than from the stage handler so it shares the fetch path's
			// protections: a concurrent getNewEnemy is dropped above, and a stop / superseding transition that
			// lands between capture and present (bumping the generation) skips it — so the prefetched idle
			// enemy can't double-spawn or clobber the fight the supersession moved to. Used only while still
			// valid (same zone, unchanged player battle-state); otherwise fall through to a fresh fetch.
			if (prepared && this.started && generation === this.fetchGeneration && this.preparedStillValid(prepared)) {
				this.currentEnemy = prepared.enemy;
				notifyNewEnemyLoaded(this.currentEnemy);
				return;
			}
			// How long the client actually simulated the battle this fetch supersedes, so the backend bounds
			// its abandon re-simulation accurately. We only reach here for a prefetched battle that was *not*
			// presented (absent or invalidated by a zone/build change during the cooldown) — the client never
			// fought it, so report 0 and the backend records no phantom outcome. A plain fetch (no prefetch,
			// e.g. after an idle loss/draw) reports the elapsed time the client did fight.
			const clientBattleMs = prepared ? 0 : battleEngine.timeElapsed;

			// Retry iteratively rather than via self-recursion: each attempt returns to a flat stack
			// (a sustained outage no longer grows the async chain without bound). The loop ends as soon
			// as `stop()` flips `started` or a transition supersedes this fetch (bumping the generation);
			// because the retry backoff is cancellable, either takes effect immediately rather than after
			// the full delay — so the controls don't feel frozen while a backoff is parked.
			while (this.started && generation === this.fetchGeneration) {
				const result = await apiSocket.sendSocketCommand('NewEnemy', {
					newZoneId: playerManager.currentZone,
					clientBattleMs
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
					// A still-in-progress battle handed back on this NewEnemy (e.g. a sub-5-minute reconnect
					// the welcome-back gate didn't resume directly) may be a boss fight the authoritative
					// PlayerState never actually abandoned — trust its isBossBattle flag over this fetch
					// loop's own idle-only assumption so the client routes into the boss loop (#1647).
					this.mode = result.data.enemyInstance.isBossBattle ? 'boss' : 'idle';
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
		// How long the client simulated the battle this challenge supersedes, so the backend bounds its abandon
		// accurately. Captured before pause() (which leaves the Loading stage): during the post-battle cooldown
		// no battle has been fought, so report 0 — otherwise the elapsed time of the in-progress idle fight.
		const clientBattleMs = battleEngine.stage === BattleStage.Loading ? 0 : battleEngine.timeElapsed;
		// Freeze the outgoing battle for the duration of the swap so it can't resolve mid-transition.
		battleEngine.pause();
		try {
			const result = await apiSocket.sendSocketCommand('ChallengeBoss', {
				zoneId: playerManager.currentZone,
				clientBattleMs
			});
			// A stop, or a later press superseding this challenge, landed while ChallengeBoss was in flight;
			// abandon so its result can't clobber the transition that took over (mirrors getNewEnemy's guard).
			if (!this.started || generation !== this.transitionGeneration) {
				return;
			}
			if (result.data?.enemyInstance) {
				this.currentEnemy = result.data.enemyInstance;
				notifyNewEnemyLoaded(this.currentEnemy);
				// Now genuinely in the boss loop, so persist boss mode iff auto-fight is armed — a pre-armed
				// toggle (set while still idle-farming) only becomes real boss-farming here. A one-off challenge
				// (auto-fight off) stays idle-persisted: a single victory hands straight back to the idle loop.
				this.syncAutoChallengeBoss(this.autoFight);
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
		// Persist boss mode only when the loop is *actually* boss-farming: auto-fight on AND already engaged in
		// the boss loop. Auto-fight can be pre-armed from BossTrigger while the loop is still idle-farming —
		// that is intent, not engagement, and must not persist boss (the offline sim would otherwise resume a
		// never-challenged player as a boss-farmer). challengeBoss() re-syncs once a pre-armed player engages.
		this.syncAutoChallengeBoss(on && this.mode === 'boss');
	}

	/**
	 * Reconciles the fresh session's live loop state with the mode the backend persisted at the player's
	 * last disconnect, read by the welcome-back gate from `GetOfflineProgress` (#1043). The live `autoFight`
	 * always starts false, so a returning boss-farmer would otherwise find their auto-fight toggle off; this
	 * re-arms it to what they left (pre-armed intent — it does not auto-engage the boss, mirroring arming the
	 * toggle from the idle BossTrigger). It also seeds the sync dedup baseline with the persisted value so a
	 * later genuine toggle still emits, while a redundant one is dropped.
	 */
	public reconcilePersistedMode(autoChallengeBoss: boolean) {
		this.autoFight = autoChallengeBoss;
		this.lastSyncedAutoChallengeBoss = autoChallengeBoss;
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
	 * loop at next login (idle vs. auto-challenge-boss). Mirrors the live auto-fight state: on ⇒ boss-farming,
	 * off ⇒ idle. The boss is always the current zone's boss, so no zone is sent — the backend keys off the
	 * player's `CurrentZoneId`. Fire-and-forget — anti-cheat validation is the server's, and a transient
	 * failure only leaves the persisted mode briefly stale, corrected by the next sync or the backend's
	 * boss-loss/draw backstop.
	 *
	 * Deduped against the last value actually sent so redundant transitions — e.g. every returnToIdle path
	 * firing when the persisted mode is already idle — don't emit a no-op socket command. The live `autoFight`
	 * starts false each session even when the persisted mode is boss; reconciling that stale-initial state on
	 * login is the welcome-back gate's job (#1043).
	 */
	private syncAutoChallengeBoss(enabled: boolean) {
		if (enabled === this.lastSyncedAutoChallengeBoss) {
			return;
		}
		this.lastSyncedAutoChallengeBoss = enabled;
		void apiSocket.sendSocketCommand('SetAutoChallengeBoss', enabled);
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
		let prepared: PreparedBattle | undefined;
		if (stage === BattleStage.Victorious && this.currentEnemy) {
			const { cooldown, nextEnemy, nextZoneId } = await this.claimVictory();
			// Capture the server-prefetched next battle (with the current player state) before waiting out the
			// cooldown, so a build change during the cooldown can be detected when deciding whether to use it.
			prepared = this.capturePreparedBattle(nextEnemy, nextZoneId);
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
			await this.getNewEnemy(prepared);
		}
	}

	/**
	 * Builds a {@link PreparedBattle} from a battle-end response's bundled next enemy, capturing the player's
	 * battle-state now so a later change can be detected. Returns undefined when the server bundled no next
	 * enemy (then {@link getNewEnemy} falls back to a normal fetch).
	 */
	private capturePreparedBattle(enemy?: IEnemyInstance, zoneId?: number): PreparedBattle | undefined {
		if (!enemy) {
			return undefined;
		}
		return {
			enemy,
			zoneId: zoneId ?? playerManager.currentZone,
			playerState: battleEngine.capturePlayerBattleState()
		};
	}

	/**
	 * Whether a prefetched battle can still be used: the player has not navigated to a different zone, and
	 * their battle-relevant state is unchanged since it was prepared. A zone change means the prefetched
	 * enemy is for the wrong zone (and the server must be told the new one); a state change would make the
	 * prefetch's frozen server snapshot diverge from what the frontend now derives (a false-rejection
	 * hazard). Either case falls back to a fresh post-cooldown fetch.
	 */
	private preparedStillValid(prepared: PreparedBattle): boolean {
		return prepared.zoneId === playerManager.currentZone && battleEngine.playerBattleStateMatches(prepared.playerState);
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
		// Capture the server-prefetched next idle battle (with the current player state) before the cooldown,
		// so a build change during the cooldown can be detected. An error response carries no `data` (e.g. a
		// transient socket failure), so guard the dereferences the same way `getNewEnemy` does — otherwise a
		// failed BattleLost would throw before the next enemy spawns and strand the player after a boss loss.
		const prepared = this.capturePreparedBattle(lostResponse.data?.nextEnemy, lostResponse.data?.nextZoneId);
		const cooldown = lostResponse.data?.cooldown ?? 0;
		if (cooldown > 0) {
			await battleEngine.startLoading(cooldown);
		}
		await this.getNewEnemy(prepared);
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
	 * and boss victory paths so the two cannot drift. Returns the post-victory cooldown (ms) and, for an
	 * idle victory, the server-prefetched next battle bundled with the response (absent for a boss victory).
	 */
	private async claimVictory(): Promise<BattleEndResult> {
		// Resolve the enemy's name up front (guarding a missing/retired id), but only log the defeat
		// after a successful DefeatEnemy so a failed command can't show "X was defeated!" with no rewards.
		const enemyId = this.currentEnemy?.id;
		const enemyName = enemyId != null ? staticData.enemies?.[enemyId]?.name : undefined;
		// Report the duration the client simulated alongside the claim. Diagnostic only (not anti-cheat):
		// the backend logs when it diverges from its own parity replay. The server validates the victory and
		// anchors the cooldown purely off its own clock, so no client timestamp is sent.
		const defeatResponse = await apiSocket.sendSocketCommand('DefeatEnemy', {
			clientTotalMs: battleEngine.timeElapsed
		});
		if (!defeatResponse.error && defeatResponse.data?.rewards) {
			if (enemyName) {
				logMessage(ELogType.EnemyDefeated, enemyName + ' was defeated!');
			}
			playerManager.applyVictoryRewards(defeatResponse.data.rewards);
		} else {
			logMessage(ELogType.Debug, 'There was an error defeating the enemy: ' + defeatResponse.error);
			if (defeatResponse.error) {
				// The transport settles a lost/timed-out response as an error even when the command may have
				// actually succeeded server-side (see docs/frontend.md's socket request lifecycle) — resync the
				// authoritative player state rather than leaving exp/level silently diverged for the rest of
				// the session. refreshPlayer only re-initializes playerManager, so the inventory (an item/mod
				// reward the lost response may have carried) is re-derived from it separately, mirroring what
				// startGame does on the initial load.
				await refreshPlayer();
				inventoryManager.initialize();
			}
		}
		// Guard `data` for a possible error response (absent `data`), now that this is the shared
		// victory path for both the idle and boss loops.
		return {
			cooldown: defeatResponse.data?.cooldown ?? 0,
			nextEnemy: defeatResponse.data?.nextEnemy,
			nextZoneId: defeatResponse.data?.nextZoneId
		};
	}
}
