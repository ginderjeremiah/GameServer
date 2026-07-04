import { ApiRequest, ELessonTriggerType, fetchSocketData, type ILesson } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { persistEntity } from '../save-helpers';
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
			// Steps aren't editable in the Workbench yet (the generic table section is numeric-cell-only;
			// authoring the free-text tour copy needs a text-capable column — filed as a follow-up).
			warn: (l) => {
				if (l.triggerType === ELessonTriggerType.MechanicEvent && l.triggerMechanicEvent === -1) {
					return 'Mechanic-event lessons must name a mechanic event';
				}
				if (l.triggerType === ELessonTriggerType.ScreenVisit && l.triggerMechanicEvent !== -1) {
					return 'Screen-visit lessons cannot also carry a mechanic-event trigger';
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
					reqMsg: 'Missing key'
				},
				{
					key: 'name',
					label: 'Lesson Name',
					type: 'text',
					placeholder: 'Name this lesson…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
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
					reqMsg: 'Missing host screen'
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
					grow: true
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
			refresh
		})
};
