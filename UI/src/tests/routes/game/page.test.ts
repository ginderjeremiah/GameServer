import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import { ELessonTriggerType } from '$lib/api';
import type { ILesson } from '$lib/api';
import { staticData } from '$stores/static-data.svelte';
import { navigation, requiresRemount } from '$stores/navigation.svelte';
import { tutorialTour } from '$stores/tutorial-tour.svelte';
import { playerManager } from '$lib/engine/player/player-manager';
import { mountTracker } from './page/screen-mount-tracker';
import FightScreenStub from './page/FightScreenStub.svelte';
import StatsScreenStub from './page/StatsScreenStub.svelte';
import NavSidebarStub from './page/NavSidebarStub.svelte';
import LogPanelStub from './page/LogPanelStub.svelte';
import TourPlayerStub from './page/TourPlayerStub.svelte';
import CharacterSwitcherStub from './page/CharacterSwitcherStub.svelte';
import BootSplashStub from './page/BootSplashStub.svelte';
import WelcomeBackGateStub from './page/WelcomeBackGateStub.svelte';
import PlaceholderScreenStub from './page/PlaceholderScreenStub.svelte';

// `+page.svelte` pulls in the live engine, socket, and every screen component; all of that is
// mocked/stubbed so the test exercises only the shell wiring: the welcome-gate phase transition,
// the screen-anchored tutorial trigger's `entered` gating (and its #1709 untrack fix), the
// navigation-intent same-screen remount, and `handleNavigate`'s action keys.
const {
	sendSocketCommand,
	getRoles,
	refreshPlayer,
	reconcilePersistedMode,
	startGame,
	stopEngines,
	confirmQuit,
	goto,
	bootState
} = vi.hoisted(() => ({
	sendSocketCommand: vi.fn(),
	getRoles: vi.fn<() => string[]>(() => []),
	refreshPlayer: vi.fn(async () => {}),
	reconcilePersistedMode: vi.fn(),
	startGame: vi.fn(),
	stopEngines: vi.fn(),
	confirmQuit: vi.fn(),
	goto: vi.fn(),
	// Defaults to already-resolved, matching every mount except a direct/refresh load of `/game`
	// (the scenario the #1898 regression test below overrides this for).
	bootState: { booted: true }
}));

vi.mock('$app/environment', () => ({ browser: true }));
vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$lib/engine/boot-state.svelte', () => ({ bootState }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand }, getRoles };
});
vi.mock('$lib/engine', () => ({
	startGame,
	stopEngines,
	enemyManager: { reconcilePersistedMode }
}));
vi.mock('$lib/engine/session', () => ({ refreshPlayer }));
// The real `evaluateScreenTrigger` is kept (wrapped as a spy) rather than mocked away: the #1709 bug
// and fix live entirely in whether the shell's `$effect` re-runs when this function's *internal*
// reads (`staticData.lessons`, `playerManager.lessons`) change, which a full mock would hide.
vi.mock('$lib/engine/tutorials', async (importOriginal) => {
	const actual = (await importOriginal()) as { evaluateScreenTrigger: (screenKey: string) => void };
	return { ...actual, evaluateScreenTrigger: vi.fn(actual.evaluateScreenTrigger) };
});
vi.mock('$stores', () => ({ navigation, staticData, tutorialTour, requiresRemount }));
vi.mock('$routes/game/game-actions', () => ({ confirmQuit }));
vi.mock('$routes/game/screens/screen-defs', () => ({
	GAME_SCREENS: [
		{ key: 'fight', label: 'Fight', group: 'combat', built: true, component: FightScreenStub },
		{ key: 'stats', label: 'Stats', group: 'character', built: true, component: StatsScreenStub },
		{ key: 'help', label: 'Help', group: 'settings', built: false },
		{ key: 'switch', label: 'Switch Character', group: 'settings', built: true },
		{ key: 'quit', label: 'Quit', group: 'settings', built: true },
		{ key: 'admin', label: 'Admin', group: 'admin', built: true, requiresRole: 'Admin' }
	],
	visibleScreens: (screens: { requiresRole?: string }[], roles: readonly string[]) =>
		screens.filter((s) => !s.requiresRole || roles.includes(s.requiresRole))
}));
vi.mock('$routes/game/screens/PlaceholderScreen.svelte', () => ({ default: PlaceholderScreenStub }));
vi.mock('$routes/game/welcome-back/WelcomeBackGate.svelte', () => ({ default: WelcomeBackGateStub }));
vi.mock('$routes/game/CharacterSwitcher.svelte', () => ({ default: CharacterSwitcherStub }));
vi.mock('$routes/BootSplash.svelte', () => ({ default: BootSplashStub }));
vi.mock('$components', () => ({ NavSidebar: NavSidebarStub, LogPanel: LogPanelStub, TourPlayer: TourPlayerStub }));

import GamePage from '../../../routes/game/+page.svelte';
import { evaluateScreenTrigger } from '$lib/engine/tutorials';

const screenVisitLesson = (overrides: Partial<ILesson> = {}): ILesson => ({
	id: 0,
	key: 'fight-intro',
	name: 'Fight Intro',
	triggerType: ELessonTriggerType.ScreenVisit,
	screenKey: 'fight',
	ordinal: 0,
	designerNotes: '',
	steps: [],
	...overrides
});

beforeEach(() => {
	vi.clearAllMocks();
	getRoles.mockReturnValue([]);
	sendSocketCommand.mockResolvedValue({ data: { hasProgress: false } });
	staticData.lessons = undefined;
	playerManager.lessons = [];
	navigation.clear();
	tutorialTour.clear();
	mountTracker.fight = 0;
	bootState.booted = true;
});

afterEach(cleanup);

describe('game +page.svelte shell', () => {
	it('gates the screen-anchored tutorial trigger on welcome.phase === "entered" and does not re-run it on unrelated lesson-state writes (#1709)', async () => {
		sendSocketCommand.mockResolvedValue({ data: { hasProgress: true } });
		staticData.lessons = [screenVisitLesson()];

		render(GamePage);

		await screen.findByTestId('welcome-gate-enter');
		expect(evaluateScreenTrigger).not.toHaveBeenCalled();

		// A lesson-state write while still gated (not yet 'entered') must not evaluate the trigger either.
		playerManager.unlockLesson(999);
		expect(evaluateScreenTrigger).not.toHaveBeenCalled();

		await fireEvent.click(screen.getByTestId('welcome-gate-enter'));

		await waitFor(() => expect(evaluateScreenTrigger).toHaveBeenCalledTimes(1));
		expect(evaluateScreenTrigger).toHaveBeenCalledWith('fight');

		// The lesson has 0 steps, so `openLesson` marks it read immediately — a write to
		// `playerManager.lessons` triggered from *inside* `evaluateScreenTrigger` itself. Before the
		// #1709 fix this write re-ran the effect (the exact bug the issue describes); it must not now.
		await waitFor(() => expect(playerManager.lessons.some((l) => l.lessonId === 0 && l.readAt)).toBe(true));
		expect(evaluateScreenTrigger).toHaveBeenCalledTimes(1);

		// A further, unrelated lesson-state write (e.g. a mechanic-anchored unlock elsewhere) must also
		// not re-trigger screen evaluation absent an actual screen change.
		playerManager.unlockLesson(1);
		expect(evaluateScreenTrigger).toHaveBeenCalledTimes(1);
	});

	it('forces a remount of the active screen on a same-screen deep-link request so its one-shot payload is consumed', async () => {
		render(GamePage);

		await waitFor(() => expect(mountTracker.fight).toBe(1));

		navigation.requestScreen('fight', { some: 'payload' });

		await waitFor(() => expect(mountTracker.fight).toBe(2));
		expect(navigation.requestedScreen).toBeNull();
	});

	it('switches screens (without a forced remount) on a cross-screen deep-link request', async () => {
		render(GamePage);
		await waitFor(() => expect(screen.getByTestId('fight-screen')).toBeTruthy());

		navigation.requestScreen('stats');

		await waitFor(() => expect(screen.queryByTestId('stats-screen')).toBeTruthy());
		expect(screen.queryByTestId('fight-screen')).toBeNull();
		expect(navigation.requestedScreen).toBeNull();
	});

	it("routes handleNavigate's action keys: admin navigates, quit confirms, switch opens (and closes) the character switcher", async () => {
		getRoles.mockReturnValue(['Admin']);

		render(GamePage);
		await waitFor(() => expect(screen.getByTestId('nav-admin')).toBeTruthy());

		await fireEvent.click(screen.getByTestId('nav-admin'));
		expect(goto).toHaveBeenCalledWith('/admin');

		await fireEvent.click(screen.getByTestId('nav-quit'));
		expect(confirmQuit).toHaveBeenCalledTimes(1);

		expect(screen.getByTestId('character-switcher').getAttribute('data-open')).toBe('false');
		await fireEvent.click(screen.getByTestId('nav-switch'));
		await waitFor(() => expect(screen.getByTestId('character-switcher').getAttribute('data-open')).toBe('true'));

		await fireEvent.click(screen.getByTestId('character-switcher-close'));
		await waitFor(() => expect(screen.getByTestId('character-switcher').getAttribute('data-open')).toBe('false'));
	});

	it('binds the sidebar pinned state two-way to the .sidebar-spacer class', async () => {
		const { container } = render(GamePage);
		await waitFor(() => expect(screen.getByTestId('nav-toggle-pinned')).toBeTruthy());

		const spacer = () => container.querySelector('.sidebar-spacer');
		expect(spacer()?.classList.contains('pinned')).toBe(false);

		await fireEvent.click(screen.getByTestId('nav-toggle-pinned'));
		await waitFor(() => expect(spacer()?.classList.contains('pinned')).toBe(true));
	});

	it('falls back to PlaceholderScreen (with the registry label) for a screen with no component', async () => {
		render(GamePage);
		await waitFor(() => expect(screen.getByTestId('fight-screen')).toBeTruthy());

		navigation.requestScreen('help');

		await waitFor(() => expect(screen.getByTestId('placeholder-screen').textContent).toBe('Help'));
	});

	it('wires TourPlayer to tutorialTour and closeTutorialTour marks the lesson read on dismiss/complete', async () => {
		const tourLesson: ILesson = {
			id: 7,
			key: 'gear-intro',
			name: 'Gear Basics',
			triggerType: ELessonTriggerType.ScreenVisit,
			screenKey: 'stats',
			ordinal: 0,
			designerNotes: '',
			steps: [{ ordinal: 0, text: 'Equip your first item.' }]
		};
		staticData.lessons = [tourLesson];

		render(GamePage);
		await waitFor(() => expect(screen.getByTestId('fight-screen')).toBeTruthy());
		expect(screen.getByTestId('tour-player').getAttribute('data-open')).toBe('false');

		navigation.requestScreen('stats');

		await waitFor(() => expect(screen.getByTestId('tour-player').getAttribute('data-open')).toBe('true'));
		expect(screen.getByTestId('tour-player').getAttribute('data-step-count')).toBe('1');
		expect(screen.getByTestId('tour-player').textContent).toContain('Gear Basics');

		await fireEvent.click(screen.getByTestId('tour-player-complete'));

		await waitFor(() => expect(screen.getByTestId('tour-player').getAttribute('data-open')).toBe('false'));
		expect(playerManager.lessons.some((l) => l.lessonId === 7 && l.readAt)).toBe(true);
	});

	it('stops the engines and cancels the welcome-back gate on unmount, so a GetOfflineProgress that resolves afterwards cannot start the game (#1807)', async () => {
		let resolveProgress: (value: { data: { hasProgress: boolean } }) => void = () => {};
		sendSocketCommand.mockImplementation(() => new Promise((resolve) => (resolveProgress = resolve)));

		const { unmount } = render(GamePage);
		await waitFor(() => expect(sendSocketCommand).toHaveBeenCalled());

		unmount();
		expect(stopEngines).toHaveBeenCalledTimes(1);

		// The GetOfflineProgress round-trip lands only now, after the page is gone.
		resolveProgress({ data: { hasProgress: false } });
		await Promise.resolve();
		await Promise.resolve();

		expect(startGame).not.toHaveBeenCalled();
	});

	it('does not run the welcome-back gate on a pre-boot mount, but does on the post-boot remount the layout always performs (#1898)', async () => {
		// A direct load/refresh of `/game` mounts this page before the root layout's boot gate has
		// resolved (children mount before the parent's onMount).
		bootState.booted = false;
		const { unmount } = render(GamePage);
		await Promise.resolve();

		expect(sendSocketCommand).not.toHaveBeenCalled();

		// The boot gate's `{#if booting}` toggle always tears this page down and remounts it once the
		// boot decision resolves — simulated here as the layout would drive it.
		unmount();
		bootState.booted = true;

		render(GamePage);
		await waitFor(() => expect(sendSocketCommand).toHaveBeenCalledWith('GetOfflineProgress'));
	});
});
