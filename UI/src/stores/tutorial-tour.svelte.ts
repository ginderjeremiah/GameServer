import type { ILesson } from '$lib/api';

/* Reactive state for the single active coach-mark tour (spike #1392). The lesson driving the tour
   (or `null` when none is playing) — `$lib/engine/tutorials.ts` owns the open/close flow that
   mutates this; `routes/game/+page.svelte` reads it to render the shared `TourPlayer`. */
let activeLesson = $state<ILesson | null>(null);

export const tutorialTour = {
	/** The lesson whose tour is currently playing, or `null` when no tour is open. */
	get activeLesson(): ILesson | null {
		return activeLesson;
	},

	/** Starts (or replaces) the active tour with `lesson`'s steps. */
	play(lesson: ILesson) {
		activeLesson = lesson;
	},

	/** Closes the active tour with no lesson playing. */
	clear() {
		activeLesson = null;
	}
};
