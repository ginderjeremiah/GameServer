/* The Help screen's reactive layer (#1589): the tutorial reading room listing every live lesson
   with its per-player locked/unread/read state. Selecting an unlocked lesson reuses the shared
   open flow (`$lib/engine/tutorials`'s `openLesson`) — navigate to its host screen and play its
   tour — the same flow a screen/mechanic trigger uses, so replaying from here behaves identically. */

import { playerManager } from '$lib/engine';
import { liveLessons, openLesson as openLessonTour } from '$lib/engine/tutorials';
import { staticData } from '$stores';

export type LessonState = 'locked' | 'unread' | 'read';

export interface LessonRowVM {
	id: number;
	name: string;
	state: LessonState;
}

export class HelpView {
	/** Every live lesson, authored order, with this player's lifecycle state. */
	readonly lessonRows = $derived.by<LessonRowVM[]>(() => {
		const playerLessons = playerManager.lessons;
		return liveLessons()
			.slice()
			.sort((a, b) => a.ordinal - b.ordinal)
			.map((lesson) => {
				const entry = playerLessons.find((pl) => pl.lessonId === lesson.id);
				const state: LessonState = !entry ? 'locked' : entry.readAt ? 'read' : 'unread';
				return { id: lesson.id, name: lesson.name, state };
			});
	});

	/** Opens the lesson's tour via the shared flow. A no-op for a locked lesson — defensive, since
	 *  a locked row isn't clickable in the first place. */
	openLesson(lessonId: number) {
		const row = this.lessonRows.find((r) => r.id === lessonId);
		if (!row || row.state === 'locked') {
			return;
		}
		// Zero-based-id catalogue — index, not `.find` (see frontend.md → Reference Data).
		const lesson = staticData.lessons?.[lessonId];
		if (lesson) {
			openLessonTour(lesson);
		}
	}
}
