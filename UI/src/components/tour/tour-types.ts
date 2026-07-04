/**
 * A single step of an anchored tutorial tour ({@link TourPlayer}). Mirrors the shape `LessonStep`
 * (#1591, backend reference data) will carry — `text` plus an optional `anchorKey` — so the player
 * can be built and tested ahead of that entity landing; a real `LessonStep` maps onto this directly
 * (its `ordinal` is just this array's position).
 */
export interface TourStep {
	/** The step's callout copy. */
	text: string;
	/** Key registered via {@link tutorialAnchor}. Omitted (or unregistered) degrades to a centered callout. */
	anchorKey?: string;
}
