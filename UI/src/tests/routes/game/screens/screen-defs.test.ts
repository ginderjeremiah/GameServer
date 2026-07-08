import { describe, it, expect } from 'vitest';
import { GAME_SCREENS, visibleScreens, type ScreenDef } from '$routes/game/screens/screen-defs';
import { ERole } from '$lib/api';
import { TOUR_ANCHOR_KEYS } from '$components';
import lessonsContent from '$content/lessons.json';

const screens: ScreenDef[] = [
	{ key: 'fight', label: 'Fight', group: 'combat', built: true },
	{ key: 'options', label: 'Options', group: 'settings', built: true },
	{ key: 'admin', label: 'Admin', group: 'admin', built: true, requiresRole: ERole.Admin }
];

describe('visibleScreens', () => {
	it('keeps all non-role-gated screens regardless of roles', () => {
		const keys = visibleScreens(screens, []).map((s) => s.key);

		expect(keys).toContain('fight');
		expect(keys).toContain('options');
	});

	it('hides a role-gated screen when the user lacks the role', () => {
		const keys = visibleScreens(screens, []).map((s) => s.key);

		expect(keys).not.toContain('admin');
	});

	it('hides a role-gated screen when the user has only unrelated roles', () => {
		const keys = visibleScreens(screens, ['Moderator']).map((s) => s.key);

		expect(keys).not.toContain('admin');
	});

	it('shows a role-gated screen when the user holds the role', () => {
		const keys = visibleScreens(screens, [ERole.Admin]).map((s) => s.key);

		expect(keys).toContain('admin');
	});
});

describe('GAME_SCREENS', () => {
	it('gates the Admin screen behind the Admin role', () => {
		const admin = GAME_SCREENS.find((s) => s.key === 'admin');

		expect(admin?.requiresRole).toBe(ERole.Admin);
	});

	it('leaves the ordinary screens ungated', () => {
		const gated = GAME_SCREENS.filter((s) => s.requiresRole).map((s) => s.key);

		expect(gated).toEqual(['admin']);
	});

	// These entries navigate or open a dialog/overlay instead of rendering a screen component, so the
	// page returns early before deriving a component for them.
	const ACTION_KEYS = ['admin', 'quit', 'switch'];

	it('gives every built, renderable screen a component (single-source registry)', () => {
		const missing = GAME_SCREENS.filter((s) => s.built && !ACTION_KEYS.includes(s.key) && !s.component).map(
			(s) => s.key
		);

		expect(missing).toEqual([]);
	});

	it('leaves "wip" placeholder screens without a component (they fall back to the placeholder)', () => {
		const wipWithComponent = GAME_SCREENS.filter((s) => !s.built && s.component).map((s) => s.key);

		expect(wipWithComponent).toEqual([]);
	});
});

// `Lesson.screenKey` is a plain string, not an FK — screens are a frontend-only registry with no
// backend representation, so the backend progression-graph lint deliberately leaves this check to
// the frontend (see ProgressionGraphChecker.CheckLessons and #1673).
describe('committed lesson content (content/lessons.json)', () => {
	const liveLessons = lessonsContent.filter((lesson) => !lesson.retiredAt);

	// Guards against the check below passing vacuously (e.g. an empty/fully-retired lessons.json)
	// with zero real coverage.
	it('has at least one live lesson to check', () => {
		expect(liveLessons.length).toBeGreaterThan(0);
	});

	it('gives every live lesson a screenKey that resolves to a real ScreenDef.key', () => {
		const screenKeys = new Set(GAME_SCREENS.map((s) => s.key));
		const badScreenKeys = liveLessons
			.filter((lesson) => !screenKeys.has(lesson.screenKey))
			.map((lesson) => `${lesson.key} -> "${lesson.screenKey}"`);

		expect(badScreenKeys).toEqual([]);
	});

	// Frontend half of the coverage #1592 called for (the backend lint can't see the DOM): every
	// anchorKey a live lesson step references must resolve to a real `use:tutorialAnchor` registration.
	// A missing anchor degrades gracefully at runtime (centered callout), so this is a content-quality
	// check, not a crash guard.
	it('gives every live lesson step an anchorKey that resolves to a registered tour anchor', () => {
		const anchorKeys = new Set(TOUR_ANCHOR_KEYS);
		const badAnchorKeys = liveLessons.flatMap((lesson) =>
			lesson.steps
				.filter((step) => step.anchorKey && !anchorKeys.has(step.anchorKey))
				.map((step) => `${lesson.key}[${step.ordinal}] -> "${step.anchorKey}"`)
		);

		expect(badAnchorKeys).toEqual([]);
	});
});
