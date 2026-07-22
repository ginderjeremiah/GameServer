import { ApiRequest, ELessonTriggerType, fetchSocketData, type ILesson } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, guardedSave, persistEntity } from '../save-helpers';
import type { EntityConfig } from './types';

/** A lesson, normalising the optional mechanic-event trigger to the select's "None" sentinel (-1) so the
 *  picker stays consistent (mirroring how zone.ts normalises its optional FK pickers). */
export interface WorkbenchLesson extends Omit<ILesson, 'triggerMechanicEvent'> {
	triggerMechanicEvent: number;
}

const refresh = async (): Promise<WorkbenchLesson[]> => {
	const lessons = await fetchSocketData('GetLessons');
	staticData.lessons = lessons;
	return lessons.map((lesson) => ({ ...lesson, triggerMechanicEvent: lesson.triggerMechanicEvent ?? -1 }));
};

export const lessonEntity: EntityConfig<WorkbenchLesson> = {
	key: 'lessons',
	label: 'Lessons',
	singular: 'Lesson',
	glyph: 'book',
	blankName: 'Untitled lesson',
	retireable: true,
	newItem: (id) => ({
		id,
		key: '',
		name: '',
		triggerType: ELessonTriggerType.ScreenVisit,
		screenKey: '',
		triggerMechanicEvent: -1,
		ordinal: 0,
		designerNotes: '',
		steps: []
	}),
	listBadge: (l) => (l.triggerType === ELessonTriggerType.ScreenVisit ? 'Screen' : 'Mechanic'),
	badgeColor: () => 'var(--accent)',
	meta: (l) => [
		['screen', l.screenKey || '—'],
		['steps', l.steps.length]
	],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Key, trigger & host screen',
			kind: 'fields',
			// Both hard-rejected by AdminLessons.SaveLessons, so they block Save (#2217).
			warn: (l) => {
				if (l.triggerType === ELessonTriggerType.MechanicEvent && l.triggerMechanicEvent === -1) {
					return { message: 'Mechanic-event lessons must name a mechanic event', blocking: true };
				}
				if (l.triggerType === ELessonTriggerType.ScreenVisit && l.triggerMechanicEvent !== -1) {
					return { message: 'Screen-visit lessons cannot also carry a mechanic-event trigger', blocking: true };
				}
				return null;
			},
			fields: [
				{
					key: 'key',
					label: 'Key',
					type: 'text',
					placeholder: 'idle-loop-basics',
					width: 220,
					required: true,
					reqMsg: 'Missing key',
					maxLength: 100
				},
				{
					key: 'name',
					label: 'Lesson Name',
					type: 'text',
					placeholder: 'Name this lesson…',
					grow: true,
					required: true,
					reqMsg: 'Missing name',
					maxLength: 100
				},
				{
					key: 'triggerType',
					label: 'Trigger',
					type: 'select',
					options: reference.lessonTriggerTypeOptions,
					width: 170
				},
				{
					key: 'screenKey',
					label: 'Host Screen',
					type: 'text',
					placeholder: 'fight',
					width: 170,
					required: true,
					reqMsg: 'Missing host screen',
					maxLength: 50
				},
				{
					key: 'triggerMechanicEvent',
					label: 'Mechanic Event',
					type: 'select',
					options: () => [{ value: -1, text: 'None' }, ...reference.mechanicEventOptions()],
					width: 200
				},
				{ key: 'ordinal', label: 'Help Screen Order', type: 'number', width: 170 },
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this lesson exists — authoring notes (never shown to players)…',
					grow: true,
					maxLength: 2000
				}
			]
		},
		{
			key: 'steps',
			label: 'Tour Steps',
			glyph: 'map',
			desc: 'The ordered coach-mark callouts this lesson’s tour plays',
			count: (l) => l.steps.length,
			// `ordinal` is both the row identity (SectionTable's rowKey) and an author-editable number —
			// unlike every other table section, which keys off a `unique` select or a surrogate id neither
			// of which an author can hand-collide. A duplicate crashes the keyed {#each} and collides in
			// the backend's ordinal-keyed reconciler, so it's caught here rather than left to blow up.
			warn: (l) => {
				if (!l.steps.length) {
					return 'No tour steps';
				}
				const ordinals = l.steps.map((s) => s.ordinal);
				// The backend's ChildCollectionReconciler rejects a duplicate desiredKey, so this blocks
				// Save (#2217); an empty set or blank callout text both save fine, so those stay advisory.
				if (new Set(ordinals).size !== ordinals.length) {
					return { message: 'Two steps share the same ordinal', blocking: true };
				}
				if (l.steps.some((s) => !s.text.trim())) {
					return 'A tour step is missing its callout text';
				}
				return null;
			},
			kind: 'table',
			itemsKey: 'steps',
			rowKey: 'ordinal',
			addLabel: 'Add step',
			emptyIcon: 'map',
			emptyTitle: 'No tour steps',
			emptySub: 'This lesson’s tour has nothing to show yet.',
			newRow: (l) => ({
				ordinal: l.steps.length ? Math.max(...l.steps.map((s) => s.ordinal)) + 1 : 0,
				text: '',
				anchorKey: undefined
			}),
			columns: [
				{ key: 'ordinal', label: 'Step', type: 'number', align: 'r', width: 90 },
				{
					key: 'text',
					label: 'Callout Text',
					type: 'text',
					min: 260,
					placeholder: 'What this step teaches the player…',
					maxLength: 500
				},
				{
					key: 'anchorKey',
					label: 'Anchor Key',
					type: 'text',
					width: 200,
					placeholder: 'Optional — centered if blank',
					optional: true
				}
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: ({
				id,
				key,
				name,
				triggerType,
				screenKey,
				triggerMechanicEvent,
				ordinal,
				designerNotes,
				retiredAt
			}) => ({
				id,
				key,
				name,
				triggerType,
				screenKey,
				// Map the "None" sentinel (-1) back to an absent mechanic-event trigger for the API.
				triggerMechanicEvent: triggerMechanicEvent === -1 ? undefined : triggerMechanicEvent,
				ordinal,
				designerNotes,
				retiredAt,
				steps: []
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditLessons', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) =>
					// SetLessonSteps reconciles against the full desired set (keyed by ordinal), not a diff —
					// mirroring SetZoneEnemies rather than the Add/Edit/Delete-changes shape SetSkillPortions
					// takes, since the backend's ChildCollectionReconciler wants the whole set every time.
					guardedSave(childChanged(record.steps, baseline?.steps), () =>
						ApiRequest.post('AdminTools/SetLessonSteps', {
							id,
							steps: record.steps.map(({ ordinal, text, anchorKey }) => ({
								ordinal,
								text,
								anchorKey: anchorKey || undefined
							}))
						})
					)
			]
		})
};
