import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, cleanup, fireEvent, screen } from '@testing-library/svelte';
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

import Help from '$routes/game/screens/help/Help.svelte';

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

afterEach(() => {
	cleanup();
	mockLessons.length = 0;
	mockPlayerLessons.length = 0;
	mockOpenLesson.mockClear();
});

describe('Help screen', () => {
	it('shows an empty state with no lessons', () => {
		render(Help);

		expect(screen.getByTestId('help-empty')).toBeTruthy();
	});

	it('lists every live lesson with its state label', () => {
		mockLessons.push(
			makeLesson({ id: 0, name: 'Locked One' }),
			makeLesson({ id: 1, name: 'Unread One', ordinal: 1 }),
			makeLesson({ id: 2, name: 'Read One', ordinal: 2 })
		);
		mockPlayerLessons.push({ lessonId: 1, unlockedAt: 'now' }, { lessonId: 2, unlockedAt: 'now', readAt: 'now' });

		render(Help);

		const locked = screen.getByTestId('help-lesson-0');
		const unread = screen.getByTestId('help-lesson-1');
		const read = screen.getByTestId('help-lesson-2');

		expect(locked.textContent).toContain('Locked One');
		expect(locked.textContent).toContain('Locked');
		expect(unread.textContent).toContain('Unread One');
		expect(unread.textContent).toContain('New');
		expect(read.textContent).toContain('Read One');
		expect(read.textContent).toContain('Replay');
	});

	it('emphasizes an unread lesson and leaves a locked one disabled', () => {
		mockLessons.push(makeLesson({ id: 0, name: 'Locked One' }), makeLesson({ id: 1, name: 'Unread One', ordinal: 1 }));
		mockPlayerLessons.push({ lessonId: 1, unlockedAt: 'now' });

		render(Help);

		expect((screen.getByTestId('help-lesson-0') as HTMLButtonElement).disabled).toBe(true);
		expect((screen.getByTestId('help-lesson-1') as HTMLButtonElement).disabled).toBe(false);
		expect(screen.getByTestId('help-lesson-1').className).toContain('unread');
	});

	it('clicking a locked lesson does not open the tour', async () => {
		mockLessons.push(makeLesson({ id: 0, name: 'Locked One' }));

		render(Help);
		await fireEvent.click(screen.getByTestId('help-lesson-0'));

		expect(mockOpenLesson).not.toHaveBeenCalled();
	});

	it('clicking an unread lesson invokes the shared open flow', async () => {
		const lesson = makeLesson({ id: 0, name: 'Unread One' });
		mockLessons.push(lesson);
		mockPlayerLessons.push({ lessonId: 0, unlockedAt: 'now' });

		render(Help);
		await fireEvent.click(screen.getByTestId('help-lesson-0'));

		expect(mockOpenLesson).toHaveBeenCalledWith(lesson);
	});

	it('clicking a read lesson replays it via the shared open flow', async () => {
		const lesson = makeLesson({ id: 0, name: 'Read One' });
		mockLessons.push(lesson);
		mockPlayerLessons.push({ lessonId: 0, unlockedAt: 'now', readAt: 'now' });

		render(Help);
		await fireEvent.click(screen.getByTestId('help-lesson-0'));

		expect(mockOpenLesson).toHaveBeenCalledWith(lesson);
	});
});
