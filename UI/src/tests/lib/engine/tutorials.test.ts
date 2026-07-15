import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ELessonTriggerType, EMechanicEvent } from '$lib/api';
import type { ILesson } from '$lib/api';
import type { SkillActivation } from '$lib/battle/battle-step';
import { staticData, navigation, tutorialTour } from '$stores';

// Controllable stub for the player-manager boundary — the same shape/behavior as the real
// unlockLesson/markLessonRead (idempotent, absent-entry-tolerant), so the locked/unread/read
// transitions under test reflect real gating rather than a hand-waved spy.
const { mockLessonsRef, mockUnlockLesson, mockMarkLessonRead } = vi.hoisted(() => {
	const mockLessonsRef: { current: { lessonId: number; unlockedAt: string; readAt?: string }[] } = {
		current: []
	};
	const mockUnlockLesson = vi.fn((lessonId: number) => {
		if (!mockLessonsRef.current.some((lesson) => lesson.lessonId === lessonId)) {
			mockLessonsRef.current.push({ lessonId, unlockedAt: 'now' });
		}
	});
	const mockMarkLessonRead = vi.fn((lessonId: number) => {
		const lesson = mockLessonsRef.current.find((l) => l.lessonId === lessonId);
		if (lesson) {
			lesson.readAt = 'now';
		} else {
			mockLessonsRef.current.push({ lessonId, unlockedAt: 'now', readAt: 'now' });
		}
	});
	return { mockLessonsRef, mockUnlockLesson, mockMarkLessonRead };
});

vi.mock('$lib/engine/player/player-manager', () => ({
	playerManager: {
		get lessons() {
			return mockLessonsRef.current;
		},
		unlockLesson: mockUnlockLesson,
		markLessonRead: mockMarkLessonRead
	}
}));

import { openLesson, closeTutorialTour, evaluateScreenTrigger, evaluateMechanicTriggers } from '$lib/engine/tutorials';

const makeLesson = (overrides: Partial<ILesson> = {}): ILesson => ({
	id: 1,
	key: 'lesson-key',
	name: 'Lesson Name',
	triggerType: ELessonTriggerType.ScreenVisit,
	screenKey: 'fight',
	ordinal: 0,
	designerNotes: '',
	steps: [{ ordinal: 0, text: 'Step text' }],
	...overrides
});

const activation = (overrides: Partial<SkillActivation> = {}): SkillActivation => ({
	skill: null as unknown as SkillActivation['skill'],
	damage: 0,
	byPlayer: true,
	crit: false,
	dodged: false,
	parried: false,
	counter: false,
	reflected: 0,
	...overrides
});

beforeEach(() => {
	mockLessonsRef.current = [];
	mockUnlockLesson.mockClear();
	mockMarkLessonRead.mockClear();
	staticData.lessons = [];
	navigation.clear();
	navigation.consumePayload();
	tutorialTour.clear();
});

describe('openLesson', () => {
	it('navigates to the lesson host screen and plays its tour', () => {
		const lesson = makeLesson({ screenKey: 'skills' });

		openLesson(lesson);

		expect(navigation.requestedScreen).toBe('skills');
		// $state wraps the assigned lesson in a reactive proxy, so compare by value, not identity.
		expect(tutorialTour.activeLesson).toEqual(lesson);
	});

	it('marks a zero-step lesson read immediately instead of opening a stuck tour (#1638)', () => {
		const lesson = makeLesson({ id: 8, steps: [] });

		openLesson(lesson);

		expect(mockMarkLessonRead).toHaveBeenCalledWith(8);
		expect(tutorialTour.activeLesson).toBeNull();
		expect(navigation.requestedScreen).toBeNull();
	});
});

describe('closeTutorialTour', () => {
	it('marks the active lesson read and clears the tour', () => {
		const lesson = makeLesson({ id: 5 });
		tutorialTour.play(lesson);

		closeTutorialTour();

		expect(tutorialTour.activeLesson).toBeNull();
		expect(mockMarkLessonRead).toHaveBeenCalledWith(5);
	});

	it('is a no-op when no tour is open', () => {
		closeTutorialTour();

		expect(mockMarkLessonRead).not.toHaveBeenCalled();
	});
});

describe('evaluateScreenTrigger', () => {
	it('opens a locked screen-anchored lesson on first activation of its screen', () => {
		const lesson = makeLesson({ id: 2, screenKey: 'attributes' });
		staticData.lessons = [lesson];

		evaluateScreenTrigger('attributes');

		expect(navigation.requestedScreen).toBe('attributes');
		expect(tutorialTour.activeLesson).toEqual(lesson);
	});

	it('does not re-fire once the lesson is already unlocked (no re-trigger on a later revisit)', () => {
		const lesson = makeLesson({ id: 2, screenKey: 'attributes' });
		staticData.lessons = [lesson];
		mockLessonsRef.current.push({ lessonId: 2, unlockedAt: 'now' });

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('does not re-fire once the lesson has been read', () => {
		const lesson = makeLesson({ id: 2, screenKey: 'attributes' });
		staticData.lessons = [lesson];
		mockLessonsRef.current.push({ lessonId: 2, unlockedAt: 'now', readAt: 'now' });

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('ignores a lesson anchored to a different screen', () => {
		const lesson = makeLesson({ id: 2, screenKey: 'inventory' });
		staticData.lessons = [lesson];

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('ignores a mechanic-anchored lesson (not a screen-visit trigger)', () => {
		const lesson = makeLesson({
			id: 2,
			screenKey: 'fight',
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		staticData.lessons = [lesson];

		evaluateScreenTrigger('fight');

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('ignores a retired lesson', () => {
		const lesson = makeLesson({ id: 2, screenKey: 'attributes', retiredAt: '2026-01-01T00:00:00Z' });
		staticData.lessons = [lesson];

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('finds a second locked lesson on the same screen even when an earlier one is already read', () => {
		const readLesson = makeLesson({ id: 2, screenKey: 'attributes' });
		const lockedLesson = makeLesson({ id: 6, screenKey: 'attributes', ordinal: 1 });
		staticData.lessons = [readLesson, lockedLesson];
		mockLessonsRef.current.push({ lessonId: 2, unlockedAt: 'now', readAt: 'now' });

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toEqual(lockedLesson);
	});

	it('does not wedge on a zero-step screen-anchored lesson, and does not re-fire on a later revisit', () => {
		const lesson = makeLesson({ id: 9, screenKey: 'attributes', steps: [] });
		staticData.lessons = [lesson];

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toBeNull();
		expect(mockMarkLessonRead).toHaveBeenCalledWith(9);

		mockMarkLessonRead.mockClear();
		evaluateScreenTrigger('attributes');

		expect(mockMarkLessonRead).not.toHaveBeenCalled();
	});
});

describe('evaluateMechanicTriggers', () => {
	it('unlocks a locked first-crit lesson when a crit lands this tick (queued unread, not displayed)', () => {
		const lesson = makeLesson({
			id: 3,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		staticData.lessons = [lesson];

		evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);

		expect(mockUnlockLesson).toHaveBeenCalledWith(3);
		// Mechanic lessons never display immediately — the player may be AFK.
		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('does not unlock when no activation matches the event', () => {
		const lesson = makeLesson({
			id: 3,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		staticData.lessons = [lesson];

		evaluateMechanicTriggers([activation({ byPlayer: true, crit: false })]);

		expect(mockUnlockLesson).not.toHaveBeenCalled();
	});

	it('does not re-fire once already unlocked (no duplicate unlock on a later crit)', () => {
		const lesson = makeLesson({
			id: 3,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		staticData.lessons = [lesson];
		mockLessonsRef.current.push({ lessonId: 3, unlockedAt: 'now' });

		evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);

		expect(mockUnlockLesson).not.toHaveBeenCalled();
	});

	it('does not re-fire once already read', () => {
		const lesson = makeLesson({
			id: 3,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		staticData.lessons = [lesson];
		mockLessonsRef.current.push({ lessonId: 3, unlockedAt: 'now', readAt: 'now' });

		evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);

		expect(mockUnlockLesson).not.toHaveBeenCalled();
	});

	it('ignores a retired lesson', () => {
		const lesson = makeLesson({
			id: 3,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit,
			retiredAt: '2026-01-01T00:00:00Z'
		});
		staticData.lessons = [lesson];

		evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);

		expect(mockUnlockLesson).not.toHaveBeenCalled();
	});

	it('only unlocks the lesson whose specific mechanic event fired', () => {
		const critLesson = makeLesson({
			id: 3,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		const dodgeLesson = makeLesson({
			id: 4,
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstDodge
		});
		staticData.lessons = [critLesson, dodgeLesson];

		evaluateMechanicTriggers([activation({ byPlayer: false, dodged: true })]);

		expect(mockUnlockLesson).toHaveBeenCalledTimes(1);
		expect(mockUnlockLesson).toHaveBeenCalledWith(4);
	});

	// #1900/#1901: evaluateMechanicTriggers memoizes "is any mechanic-anchored lesson still locked"
	// so the steady state (everything already unlocked) can skip its per-tick scan. These pin that the
	// memoization never skips a tick that still has real work to do.
	describe('locked-lesson memoization', () => {
		it('keeps evaluating on a later tick while one of two lessons is still locked', () => {
			const critLesson = makeLesson({
				id: 3,
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstCrit
			});
			const dodgeLesson = makeLesson({
				id: 4,
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstDodge
			});
			staticData.lessons = [critLesson, dodgeLesson];

			// Tick 1: only the crit lesson fires — the dodge lesson stays locked, so the "any locked
			// mechanic lesson" flag must not flip to exhausted.
			evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);
			expect(mockUnlockLesson).toHaveBeenCalledWith(3);
			mockUnlockLesson.mockClear();

			// Tick 2, same staticData/player-lesson references: the dodge lesson must still be evaluated
			// and unlocked, not skipped by a cache that wrongly assumed nothing was left locked.
			evaluateMechanicTriggers([activation({ byPlayer: false, dodged: true })]);
			expect(mockUnlockLesson).toHaveBeenCalledWith(4);
		});

		it('does not re-fire on a later tick once every mechanic lesson has unlocked', () => {
			const lesson = makeLesson({
				id: 3,
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstCrit
			});
			staticData.lessons = [lesson];

			evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);
			expect(mockUnlockLesson).toHaveBeenCalledTimes(1);
			mockUnlockLesson.mockClear();

			// Steady state: further ticks (even ones that would otherwise match) must stay a no-op.
			evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);
			expect(mockUnlockLesson).not.toHaveBeenCalled();
		});

		it('recomputes after a reference-data reload adds a new mechanic lesson past exhaustion', () => {
			const critLesson = makeLesson({
				id: 3,
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstCrit
			});
			staticData.lessons = [critLesson];

			// Exhaust the cache: the only mechanic lesson unlocks, so the memoized flag flips to "none
			// locked remain".
			evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);
			expect(mockUnlockLesson).toHaveBeenCalledTimes(1);
			mockUnlockLesson.mockClear();

			// Reference data reloads with a new locked mechanic lesson — a new array reference, not a
			// mutation of the old one, mirroring how the reference-data cache actually replaces its data.
			const dodgeLesson = makeLesson({
				id: 4,
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstDodge
			});
			staticData.lessons = [critLesson, dodgeLesson];

			evaluateMechanicTriggers([activation({ byPlayer: false, dodged: true })]);

			expect(mockUnlockLesson).toHaveBeenCalledWith(4);
		});

		it('recomputes after the player-lesson array is replaced wholesale (e.g. refreshPlayer re-initializing the player)', () => {
			const lesson = makeLesson({
				id: 3,
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstCrit
			});
			staticData.lessons = [lesson];

			evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);
			expect(mockUnlockLesson).toHaveBeenCalledTimes(1);
			mockUnlockLesson.mockClear();

			// Simulate PlayerManager.initialize reassigning `lessons` to a freshly-fetched array (a new
			// reference) rather than mutating the existing one in place.
			mockLessonsRef.current = [];

			evaluateMechanicTriggers([activation({ byPlayer: true, crit: true })]);

			expect(mockUnlockLesson).toHaveBeenCalledWith(3);
		});
	});
});
