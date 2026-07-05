import { ELessonTriggerType, EMechanicEvent, type ILesson } from '$lib/api';
import { isFirstCooldownRecharge, isFirstCrit, isFirstDodge } from '$lib/common/content-events';
import type { SkillActivation } from '$lib/battle/battle-step';
import { navigation, staticData, tutorialTour } from '$stores';
import { playerManager } from './player/player-manager';

/** Live (non-retired) lessons from reference data, or `[]` before it has loaded. */
const liveLessons = (): ILesson[] => (staticData.lessons ?? []).filter((lesson) => !lesson.retiredAt);

/** A lesson is locked until the player has an entry for it (unlocked or read) — see
 *  {@link PlayerManager.unlockLesson}/{@link PlayerManager.markLessonRead}. */
const isLocked = (lessonId: number): boolean => !playerManager.lessons.some((lesson) => lesson.lessonId === lessonId);

/**
 * Opens `lesson`'s tour: navigates to its host screen (a no-op if already there) and plays its
 * steps. This is the single open→navigate→play flow shared by the screen-anchored auto-trigger
 * below and manually opening a queued unread lesson (e.g. from the Help nav badge, #1589).
 */
export function openLesson(lesson: ILesson) {
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

/**
 * Mechanic-anchored trigger (spike #1392, #1587): call with each battle tick's activations (wired
 * into `BattleEngine.logicalUpdate`). A locked mechanic lesson whose event fires this tick is
 * unlocked — queued as unread rather than displayed immediately, since the player may be AFK.
 * `isLocked` gating stands in for a "was this true before" flag: once a lesson unlocks, it drops
 * out of consideration on every later tick, so a mechanic that keeps recurring (e.g. further crits)
 * can't re-fire it.
 */
export function evaluateMechanicTriggers(activations: readonly SkillActivation[]) {
	for (const lesson of liveLessons()) {
		if (
			lesson.triggerType === ELessonTriggerType.MechanicEvent &&
			isLocked(lesson.id) &&
			mechanicFired(lesson.triggerMechanicEvent, activations)
		) {
			playerManager.unlockLesson(lesson.id);
		}
	}
}
