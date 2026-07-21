import { describe, it, expect, vi } from 'vitest';
import { ELessonTriggerType, type ILesson, type IPlayerLesson } from '$lib/api';

const { mockLessons, mockPlayerLessons, mockOpenLesson } = vi.hoisted(() => ({
	mockLessons: [] as ILesson[],
	mockPlayerLessons: [] as IPlayerLesson[],
	mockOpenLesson: vi.fn()
}));

vi.mock('$lib/engine', () => ({
	playerManager: {
		get lessons() {
			return mockPlayerLessons;
		}
	}
}));

vi.mock('$lib/engine/tutorials', () => ({
	liveLessons: () => mockLessons.filter((l) => !l.retiredAt),
	openLesson: mockOpenLesson
}));

vi.mock('$stores', () => ({
	get staticData() {
		return { lessons: mockLessons };
	}
}));

import { HelpView } from '$routes/game/screens/help/help-view.svelte';

const makeLesson = (overrides: Partial<ILesson> = {}): ILesson => ({
	id: 0,
	key: 'lesson-key',
	name: 'Lesson Name',
	triggerType: ELessonTriggerType.ScreenVisit,
	screenKey: 'fight',
	ordinal: 0,
	designerNotes: '',
	steps: [{ ordinal: 0, text: 'Step text' }],
	...overrides
});

describe('HelpView.lessonRows', () => {
	it('classifies a lesson with no player entry as locked', () => {
		mockLessons.length = 0;
		mockLessons.push(makeLesson({ id: 0, name: 'Alpha' }));
		mockPlayerLessons.length = 0;

		const view = new HelpView();

		expect(view.lessonRows).toEqual([{ id: 0, name: 'Alpha', state: 'locked' }]);
	});

	it('classifies an unlocked-but-unread lesson as unread', () => {
		mockLessons.length = 0;
		mockLessons.push(makeLesson({ id: 0, name: 'Alpha' }));
		mockPlayerLessons.length = 0;
		mockPlayerLessons.push({ lessonId: 0, unlockedAt: 'now' });

		const view = new HelpView();

		expect(view.lessonRows[0].state).toBe('unread');
	});

	it('classifies a lesson with a readAt as read', () => {
		mockLessons.length = 0;
		mockLessons.push(makeLesson({ id: 0, name: 'Alpha' }));
		mockPlayerLessons.length = 0;
		mockPlayerLessons.push({ lessonId: 0, unlockedAt: 'now', readAt: 'now' });

		const view = new HelpView();

		expect(view.lessonRows[0].state).toBe('read');
	});

	it('sorts rows by ordinal regardless of catalogue order', () => {
		mockLessons.length = 0;
		mockLessons.push(
			makeLesson({ id: 0, name: 'Second', ordinal: 1 }),
			makeLesson({ id: 1, name: 'First', ordinal: 0 })
		);
		mockPlayerLessons.length = 0;

		const view = new HelpView();

		expect(view.lessonRows.map((r) => r.name)).toEqual(['First', 'Second']);
	});

	it('excludes retired lessons', () => {
		mockLessons.length = 0;
		mockLessons.push(makeLesson({ id: 0, name: 'Alpha' }), makeLesson({ id: 1, name: 'Retired', retiredAt: 'now' }));
		mockPlayerLessons.length = 0;

		const view = new HelpView();

		expect(view.lessonRows.map((r) => r.name)).toEqual(['Alpha']);
	});
});

describe('HelpView.openLesson', () => {
	it('is a no-op for a locked lesson', () => {
		mockLessons.length = 0;
		mockLessons.push(makeLesson({ id: 0, name: 'Alpha' }));
		mockPlayerLessons.length = 0;
		mockOpenLesson.mockClear();

		const view = new HelpView();
		view.openLesson(0);

		expect(mockOpenLesson).not.toHaveBeenCalled();
	});

	it('delegates to the shared open flow for an unread lesson', () => {
		mockLessons.length = 0;
		const lesson = makeLesson({ id: 0, name: 'Alpha' });
		mockLessons.push(lesson);
		mockPlayerLessons.length = 0;
		mockPlayerLessons.push({ lessonId: 0, unlockedAt: 'now' });
		mockOpenLesson.mockClear();

		const view = new HelpView();
		view.openLesson(0);

		expect(mockOpenLesson).toHaveBeenCalledWith(lesson);
	});

	it('delegates to the shared open flow for a read lesson (replay)', () => {
		mockLessons.length = 0;
		const lesson = makeLesson({ id: 0, name: 'Alpha' });
		mockLessons.push(lesson);
		mockPlayerLessons.length = 0;
		mockPlayerLessons.push({ lessonId: 0, unlockedAt: 'now', readAt: 'now' });
		mockOpenLesson.mockClear();

		const view = new HelpView();
		view.openLesson(0);

		expect(mockOpenLesson).toHaveBeenCalledWith(lesson);
	});

	it('is a no-op for an unknown lesson id', () => {
		mockLessons.length = 0;
		mockPlayerLessons.length = 0;
		mockOpenLesson.mockClear();

		const view = new HelpView();
		view.openLesson(99);

		expect(mockOpenLesson).not.toHaveBeenCalled();
	});
});
