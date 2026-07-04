import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ELessonTriggerType, EMechanicEvent, type ILesson } from '$lib/api';

/* Lesson config transforms: newItem's blank record, the -1 "None" sentinel round-trip for the optional
   mechanic-event trigger (refresh in, persist out), the trigger-consistency warning, and the derived
   display data. The fetchSocketData/ApiRequest boundary is stubbed; persistEntity runs real. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { lessons: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetLessons' ? socket.lessons : []))
	};
});

vi.mock('$stores', () => ({ staticData }));
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

const screenVisitLesson = (): ILesson => ({
	id: 0,
	key: 'idle-loop-basics',
	name: 'Idle Combat',
	triggerType: ELessonTriggerType.ScreenVisit,
	screenKey: 'fight',
	ordinal: 0,
	designerNotes: '',
	steps: []
});

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.lessons = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('lessonEntity', () => {
	it('newItem starts as a screen-visit lesson with no mechanic event', () => {
		const l = lessonEntity.newItem(3);
		expect(l).toMatchObject({
			id: 3,
			key: '',
			name: '',
			triggerType: ELessonTriggerType.ScreenVisit,
			screenKey: '',
			triggerMechanicEvent: -1,
			steps: []
		});
	});

	it('listBadge reports the trigger kind', () => {
		expect(lessonEntity.listBadge?.(lessonEntity.newItem(1))).toBe('Screen');
		expect(
			lessonEntity.listBadge?.({ ...lessonEntity.newItem(1), triggerType: ELessonTriggerType.MechanicEvent })
		).toBe('Mechanic');
	});

	it('meta reports the host screen and step count', () => {
		const l = { ...lessonEntity.newItem(1), screenKey: 'fight', steps: [{ ordinal: 0, text: 'Step' }] };
		expect(lessonEntity.meta(l)).toEqual([
			['screen', 'fight'],
			['steps', 1]
		]);
	});

	it('refresh normalises an absent mechanic-event trigger to the -1 sentinel', async () => {
		socket.lessons = [screenVisitLesson()];

		const lessons = await lessonEntity.refresh();

		expect(lessons[0].triggerMechanicEvent).toBe(-1);
		expect(staticData.lessons).toEqual(socket.lessons);
	});

	it('refresh preserves a real mechanic-event trigger', async () => {
		socket.lessons = [
			{
				...screenVisitLesson(),
				triggerType: ELessonTriggerType.MechanicEvent,
				triggerMechanicEvent: EMechanicEvent.FirstCrit
			}
		];

		const lessons = await lessonEntity.refresh();

		expect(lessons[0].triggerMechanicEvent).toBe(EMechanicEvent.FirstCrit);
	});

	it('warns when a mechanic-event lesson names no mechanic event', () => {
		const identity = lessonEntity.sections.find((s) => s.key === 'identity');
		const l = { ...lessonEntity.newItem(0), triggerType: ELessonTriggerType.MechanicEvent, triggerMechanicEvent: -1 };
		expect(identity && 'warn' in identity ? identity.warn?.(l) : null).toBe(
			'Mechanic-event lessons must name a mechanic event'
		);
	});

	it('warns when a screen-visit lesson also carries a mechanic-event trigger', () => {
		const identity = lessonEntity.sections.find((s) => s.key === 'identity');
		const l = { ...lessonEntity.newItem(0), triggerMechanicEvent: EMechanicEvent.FirstDodge };
		expect(identity && 'warn' in identity ? identity.warn?.(l) : null).toBe(
			'Screen-visit lessons cannot also carry a mechanic-event trigger'
		);
	});

	it('does not warn on a consistent trigger', () => {
		const identity = lessonEntity.sections.find((s) => s.key === 'identity');
		expect(identity && 'warn' in identity ? identity.warn?.(lessonEntity.newItem(0)) : null).toBeNull();
	});

	it('persist maps the -1 sentinel back to an absent mechanic-event trigger and drops steps', async () => {
		const record = { ...lessonEntity.newItem(0), key: 'idle-loop-basics', name: 'New', triggerMechanicEvent: -1 };
		const baseline = { ...record, name: 'Old' };
		socket.lessons = [screenVisitLesson()];

		await lessonEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		const item = postBodyTo('AdminTools/AddEditLessons')[0].item;
		expect(item.triggerMechanicEvent).toBeUndefined();
		expect(item.steps).toEqual([]);
	});

	it('persist passes through a real mechanic-event trigger', async () => {
		const record = {
			...lessonEntity.newItem(0),
			key: 'crit-dodge-variance',
			name: 'New',
			triggerType: ELessonTriggerType.MechanicEvent,
			triggerMechanicEvent: EMechanicEvent.FirstCrit
		};
		const baseline = { ...record, name: 'Old' };
		socket.lessons = [screenVisitLesson()];

		await lessonEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		const item = postBodyTo('AdminTools/AddEditLessons')[0].item;
		expect(item.triggerMechanicEvent).toBe(EMechanicEvent.FirstCrit);
	});
});
