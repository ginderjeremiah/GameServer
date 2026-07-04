import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EChangeType, ELessonTriggerType, EMechanicEvent, type ILesson } from '$lib/api';
import type { LessonStepsSectionConfig, LessonTriggerSectionConfig } from '$routes/admin/workbench/entities/types';

/* Lesson config transforms: `newItem` defaults (screen-visit trigger on the first game screen), the
   headline/meta, the trigger section's warn predicate (mutually-exclusive trigger fields), the steps
   section's warn predicate, and the persist path — the identity save carries every scalar field while
   the steps collection goes through its own setter, an untouched steps collection is skipped, and a
   steps-only change must not hit the identity Add/Edit endpoint. `fetchSocketData`/`ApiRequest` are
   stubbed; the real `persistEntity` orchestration runs unmocked. */

const { socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { lessons: [] as unknown[] };
	return {
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetLessons' ? socket.lessons : []))
	};
});

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static post = mockPost;
		static get = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetch };
});

import { lessonEntity } from '$routes/admin/workbench/entities/lesson';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

const section = <K extends string>(key: K) => lessonEntity.sections.find((s) => s.key === key);
const triggerSection = () => section('trigger') as LessonTriggerSectionConfig<ILesson>;
const stepsSection = () => section('steps') as LessonStepsSectionConfig<ILesson>;

const lesson = (over: Partial<ILesson> = {}): ILesson => ({
	id: 0,
	key: 'first-crit',
	name: 'First Crit',
	triggerType: ELessonTriggerType.ScreenVisit,
	triggerScreenKey: 'fight',
	triggerMechanicEvent: undefined,
	hostScreenKey: 'fight',
	displayOrder: 0,
	steps: [{ text: 'Land a critical hit to see it in action.' }],
	...over
});

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.lessons = [];
});

describe('lessonEntity', () => {
	it('newItem defaults to a screen-visit trigger on the first game screen with no steps', () => {
		const item = lessonEntity.newItem(7);
		expect(item).toMatchObject({
			id: 7,
			key: '',
			name: '',
			triggerType: ELessonTriggerType.ScreenVisit,
			triggerMechanicEvent: undefined,
			displayOrder: 0,
			steps: []
		});
		// Both screen-key fields default to the same (first) game screen, and it's a real, non-empty key.
		expect(item.triggerScreenKey).toBe(item.hostScreenKey);
		expect(item.hostScreenKey).toBeTruthy();
	});

	it('headline shows the authoring key; meta shows the step count', () => {
		expect(lessonEntity.headline?.(lesson())).toBe('first-crit');
		expect(lessonEntity.meta(lesson({ steps: [{ text: 'a' }, { text: 'b' }] }))).toEqual([['steps', 2]]);
	});

	it('warns when a screen-visit lesson has no trigger screen', () => {
		const warn = triggerSection().warn;
		expect(warn?.(lesson({ triggerType: ELessonTriggerType.ScreenVisit, triggerScreenKey: 'fight' }))).toBeNull();
		expect(warn?.(lesson({ triggerType: ELessonTriggerType.ScreenVisit, triggerScreenKey: undefined }))).toBe(
			'Screen-visit lessons need a trigger screen'
		);
	});

	it('warns when a mechanic-event lesson has no trigger event', () => {
		const warn = triggerSection().warn;
		const mechanicLesson = lesson({
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerScreenKey: undefined,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		});
		expect(warn?.(mechanicLesson)).toBeNull();
		expect(warn?.({ ...mechanicLesson, triggerMechanicEvent: undefined })).toBe(
			'Mechanic-event lessons need a trigger event'
		);
	});

	it('warns when there are no steps', () => {
		const warn = stepsSection().warn;
		expect(warn?.(lesson({ steps: [{ text: 'a' }] }))).toBeNull();
		expect(warn?.(lesson({ steps: [] }))).toBe('A lesson must have at least one step');
	});

	it('persist saves the steps when only steps change, without an identity Edit', async () => {
		const baseline = lesson({ id: 0, steps: [{ text: 'old' }] });
		const record = lesson({ id: 0, steps: [{ text: 'old' }, { text: 'new' }] });
		socket.lessons = [record];

		await lessonEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		expect(postBodyTo('AdminTools/AddEditLessons')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetLessonSteps')).toEqual({ id: 0, steps: record.steps });
	});

	it('persist sends an identity Edit (steps stripped) when a scalar field changes', async () => {
		const baseline = lesson({ id: 0, name: 'First Crit', steps: [{ text: 'a' }] });
		const record = lesson({ id: 0, name: 'First Critical Hit', steps: [{ text: 'a' }] });
		socket.lessons = [record];

		await lessonEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		const edit = postBodyTo('AdminTools/AddEditLessons');
		expect(edit[0].changeType).toBe(EChangeType.Edit);
		expect(edit[0].item).toMatchObject({ id: 0, name: 'First Critical Hit', steps: [] });
		// Steps didn't change, so the steps setter is skipped.
		expect(postBodyTo('AdminTools/SetLessonSteps')).toBeUndefined();
	});

	it('persist Adds a new lesson and saves its steps against the resolved id', async () => {
		const added = lesson({ id: -1, steps: [{ text: 'a' }, { text: 'b', anchorKey: 'skill-bar' }] });
		socket.lessons = [{ ...added, id: 9 }]; // the persisted record at its real id

		await lessonEntity.persist({ added: [added], modified: [], deleted: [], existingIds: [] });

		const add = postBodyTo('AdminTools/AddEditLessons');
		expect(add[0].changeType).toBe(EChangeType.Add);
		expect(add[0].item).toMatchObject({ id: -1, key: 'first-crit', steps: [] });
		// The steps setter targets the resolved real id (9), not the temporary -1.
		expect(postBodyTo('AdminTools/SetLessonSteps')).toEqual({ id: 9, steps: added.steps });
	});
});
