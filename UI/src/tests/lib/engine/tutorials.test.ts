import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ELessonTriggerType, EMechanicEvent } from '$lib/api';
import type { ILesson } from '$lib/api';
import type { SkillActivation } from '$lib/battle/battle-step';
import { staticData, navigation, tutorialTour } from '$stores';

// Controllable stub for the player-manager boundary — the same shape/behavior as the real
// unlockLesson/markLessonRead (idempotent, absent-entry-tolerant), so the locked/unread/read
// transitions under test reflect real gating rather than a hand-waved spy.
const { mockLessons, mockUnlockLesson, mockMarkLessonRead } = vi.hoisted(() => {
	const mockLessons: { lessonId: number; unlockedAt: string; readAt?: string }[] = [];
	const mockUnlockLesson = vi.fn((lessonId: number) => {
		if (!mockLessons.some((lesson) => lesson.lessonId === lessonId)) {
			mockLessons.push({ lessonId, unlockedAt: 'now' });
		}
	});
	const mockMarkLessonRead = vi.fn((lessonId: number) => {
		const lesson = mockLessons.find((l) => l.lessonId === lessonId);
		if (lesson) {
			lesson.readAt = 'now';
		} else {
			mockLessons.push({ lessonId, unlockedAt: 'now', readAt: 'now' });
		}
	});
	return { mockLessons, mockUnlockLesson, mockMarkLessonRead };
});

vi.mock('$lib/engine/player/player-manager', () => ({
	playerManager: {
		get lessons() {
			return mockLessons;
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
	steps: [],
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
	mockLessons.length = 0;
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
		mockLessons.push({ lessonId: 2, unlockedAt: 'now' });

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toBeNull();
	});

	it('does not re-fire once the lesson has been read', () => {
		const lesson = makeLesson({ id: 2, screenKey: 'attributes' });
		staticData.lessons = [lesson];
		mockLessons.push({ lessonId: 2, unlockedAt: 'now', readAt: 'now' });

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
		mockLessons.push({ lessonId: 2, unlockedAt: 'now', readAt: 'now' });

		evaluateScreenTrigger('attributes');

		expect(tutorialTour.activeLesson).toEqual(lockedLesson);
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
		mockLessons.push({ lessonId: 3, unlockedAt: 'now' });

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
		mockLessons.push({ lessonId: 3, unlockedAt: 'now', readAt: 'now' });

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
});
