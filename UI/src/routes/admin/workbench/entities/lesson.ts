import { ApiRequest, ELessonTriggerType, fetchSocketData, type ILesson } from '$lib/api';
import { GAME_SCREENS } from '../../../game/screens/screen-defs';
import { childChanged, persistEntity } from '../save-helpers';
import type { EntityConfig } from './types';

/**
 * The admin Workbench editor for in-game tutorial lessons (issue #1591). A lesson fires either on a
 * screen visit or a mechanic event (mutually exclusive — enforced client-side by
 * {@link ../components/lesson/LessonTriggerSection.svelte} and server-side by the backend's
 * `FindTriggerViolation` check) and hosts an ordered list of tutorial steps, persisted through their
 * own setter since array position (not a stored id/order) is the step order.
 *
 * Screen keys have no backend registry — screens are a frontend-only concept — so this is the one
 * place a screen key gets real validation: both screen pickers read options from {@link GAME_SCREENS}.
 */
const refresh = (): Promise<ILesson[]> => fetchSocketData('GetLessons');

export const lessonEntity: EntityConfig<ILesson> = {
	key: 'lessons',
	label: 'Lessons',
	singular: 'Lesson',
	// No book/lightbulb glyph exists in the shared set; `pin` (a location marker) reads reasonably as
	// "points something out to the player," which is what an anchored tutorial step does.
	glyph: 'pin',
	blankName: 'New lesson',
	retireable: true,
	newItem: (id) => ({
		id,
		key: '',
		name: '',
		triggerType: ELessonTriggerType.ScreenVisit,
		triggerScreenKey: GAME_SCREENS[0]?.key,
		triggerMechanicEvent: undefined,
		hostScreenKey: GAME_SCREENS[0]?.key ?? '',
		displayOrder: 0,
		steps: []
	}),
	// `headline` shows the authoring key as a one-line preview; the title itself falls back to `name`.
	headline: (l) => l.key,
	meta: (l) => [['steps', l.steps.length]],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Authoring key, player-facing name & list ordering',
			kind: 'fields',
			fields: [
				{
					key: 'key',
					label: 'Key',
					type: 'text',
					placeholder: 'e.g. first-crit',
					required: true,
					reqMsg: 'Missing key'
				},
				{
					key: 'name',
					label: 'Name',
					type: 'text',
					placeholder: 'Shown in the Help screen',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{ key: 'displayOrder', label: 'Display Order', type: 'number', width: 120 }
			]
		},
		{
			key: 'trigger',
			label: 'Trigger',
			glyph: 'bolt',
			desc: 'Host screen & what fires the lesson',
			kind: 'lesson-trigger',
			dirtyKeys: ['hostScreenKey', 'triggerType', 'triggerScreenKey', 'triggerMechanicEvent'],
			warn: (l) =>
				l.triggerType === ELessonTriggerType.ScreenVisit && !l.triggerScreenKey
					? 'Screen-visit lessons need a trigger screen'
					: l.triggerType === ELessonTriggerType.MechanicEvent && l.triggerMechanicEvent === undefined
						? 'Mechanic-event lessons need a trigger event'
						: null
		},
		{
			key: 'steps',
			label: 'Steps',
			glyph: 'bars',
			desc: 'The ordered tutorial callouts shown to the player',
			kind: 'lesson-steps',
			count: (l) => l.steps.length,
			dirtyKeys: ['steps'],
			warn: (l) => (l.steps.length === 0 ? 'A lesson must have at least one step' : null)
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			// The identity DTO carries every scalar field; the steps collection is saved through its own
			// setter, so it's emptied here. The C# contract requires the key, so it's sent (empty) rather
			// than omitted.
			toPrimaryDto: (l) => ({
				id: l.id,
				key: l.key,
				name: l.name,
				triggerType: l.triggerType,
				triggerScreenKey: l.triggerScreenKey,
				triggerMechanicEvent: l.triggerMechanicEvent,
				hostScreenKey: l.hostScreenKey,
				displayOrder: l.displayOrder,
				retiredAt: l.retiredAt,
				steps: []
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditLessons', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					if (childChanged(record.steps, baseline?.steps)) {
						await ApiRequest.post('AdminTools/SetLessonSteps', { id, steps: record.steps });
					}
				}
			]
		})
};
