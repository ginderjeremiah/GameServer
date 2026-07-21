import { ELessonTriggerType, EMechanicEvent, type ILesson, type IPlayerLesson } from '$lib/api';
import { isFirstCooldownRecharge, isFirstCrit, isFirstDodge } from '$lib/common/content-events';
import type { SkillActivation } from '$lib/battle/battle-step';
import { navigation, staticData, tutorialTour } from '$stores';
import { playerManager } from './player/player-manager';

/** Live (non-retired) lessons from reference data, or `[]` before it has loaded. */
export const liveLessons = (): ILesson[] => (staticData.lessons ?? []).filter((lesson) => !lesson.retiredAt);

/** A lesson is locked until the player has an entry for it (unlocked or read) — see
 *  {@link PlayerManager.unlockLesson}/{@link PlayerManager.markLessonRead}. */
const isLocked = (lessonId: number): boolean => !playerManager.lessons.some((lesson) => lesson.lessonId === lessonId);

/**
 * Opens `lesson`'s tour: navigates to its host screen (a no-op if already there) and plays its
 * steps. This is the single open→navigate→play flow shared by the screen-anchored auto-trigger
 * below and manually opening a queued unread lesson (e.g. from the Help nav badge, #1589).
 *
 * A lesson with no steps can't render `TourPlayer` (it has nothing to show as `currentStep`), so
 * its dismiss/complete callbacks — the only path that marks a lesson read — would never fire.
 * Rather than wedge as a permanently "active" lesson that keeps re-triggering, treat it as a no-op
 * and mark it read immediately so bad content degrades gracefully instead of getting stuck.
 */
export function openLesson(lesson: ILesson) {
	if (lesson.steps.length === 0) {
		playerManager.markLessonRead(lesson.id);
		return;
	}
	navigation.requestScreen(lesson.screenKey);
	tutorialTour.play(lesson);
}

/**
 * Closes the active tour and marks its lesson read — called on both early dismissal (Escape/
 * backdrop/Skip) and finishing the last step, since either way the player has seen it (Skip means
 * "got it", not "remind me later"). A no-op if no tour is open.
 */
export function closeTutorialTour() {
	const lesson = tutorialTour.activeLesson;
	if (!lesson) {
		return;
	}
	tutorialTour.clear();
	playerManager.markLessonRead(lesson.id);
}

/**
 * Screen-anchored trigger (spike #1392, #1587): call whenever the active in-game screen changes,
 * passing the newly-activated screen's key — the single activation point cross-screen navigation
 * already uses (`routes/game/+page.svelte`; screens are registry state, not routes). The first
 * activation of a screen with a locked screen-anchored lesson plays its tour immediately, since the
 * player just navigated there.
 */
export function evaluateScreenTrigger(screenKey: string) {
	const lesson = liveLessons().find(
		(candidate) =>
			candidate.triggerType === ELessonTriggerType.ScreenVisit &&
			candidate.screenKey === screenKey &&
			isLocked(candidate.id)
	);
	if (lesson) {
		openLesson(lesson);
	}
}

/** Whether `event` occurred among this tick's activations, per the shared content-events detectors. */
const mechanicFired = (
	event: EMechanicEvent | undefined,
	activations: readonly Pick<SkillActivation, 'byPlayer' | 'crit' | 'dodged' | 'counter'>[]
): boolean => {
	switch (event) {
		case EMechanicEvent.FirstCrit:
			return isFirstCrit(false, activations);
		case EMechanicEvent.FirstDodge:
			return isFirstDodge(false, activations);
		case EMechanicEvent.FirstCooldownRecharge:
			return isFirstCooldownRecharge(false, activations);
		default:
			return false;
	}
};

// Memoized "is there still a locked mechanic-anchored lesson" flag for evaluateMechanicTriggers
// below, keyed against the two sources it derives from (#1900/#1901). Once every mechanic lesson
// has unlocked — the common steady state past onboarding — this lets the hot tick loop skip
// liveLessons()'s filter allocation and isLocked's linear scan entirely. Re-armed whenever either
// source's reference changes (a reference-data reload, or the player data being re-initialized —
// e.g. the welcome-back gate's refreshPlayer) rather than trusting the cached verdict to still hold.
let mechanicLessonsSource: ILesson[] | undefined;
let mechanicLessonsPlayerSource: IPlayerLesson[] | undefined;
let anyLockedMechanicLesson = true;

/**
 * Mechanic-anchored trigger (spike #1392, #1587): call with each battle tick's activations (wired
 * into `BattleEngine.logicalUpdate`). A locked mechanic lesson whose event fires this tick is
 * unlocked — queued as unread rather than displayed immediately, since the player may be AFK.
 * `isLocked` gating stands in for a "was this true before" flag: once a lesson unlocks, it drops
 * out of consideration on every later tick, so a mechanic that keeps recurring (e.g. further crits)
 * can't re-fire it.
 */
export function evaluateMechanicTriggers(activations: readonly SkillActivation[]) {
	if (staticData.lessons !== mechanicLessonsSource || playerManager.lessons !== mechanicLessonsPlayerSource) {
		mechanicLessonsSource = staticData.lessons;
		mechanicLessonsPlayerSource = playerManager.lessons;
		anyLockedMechanicLesson = true;
	}
	if (!anyLockedMechanicLesson) {
		return;
	}

	let stillLocked = false;
	for (const lesson of liveLessons()) {
		if (lesson.triggerType !== ELessonTriggerType.MechanicEvent || !isLocked(lesson.id)) {
			continue;
		}
		if (mechanicFired(lesson.triggerMechanicEvent, activations)) {
			playerManager.unlockLesson(lesson.id);
		} else {
			stillLocked = true;
		}
	}
	anyLockedMechanicLesson = stillLocked;
}
