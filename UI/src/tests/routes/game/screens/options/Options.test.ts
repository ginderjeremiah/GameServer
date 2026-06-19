import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { type ILogPreference } from '$lib/api';

// The screen builds its own OptionsView at mount, so it needs the same engine/stores/api/$components
// stand-ins the view-model and subcomponent tests use. `prefersReducedMotion` is forced on so the
// live preview takes its static path (no timers/rAF) and the smoke render stays deterministic.
const { mockPlayerManager, sendSocketCommand, toastError, prefersReducedMotion } = vi.hoisted(() => ({
	mockPlayerManager: { logPreferences: [] as ILogPreference[] },
	sendSocketCommand: vi.fn(),
	toastError: vi.fn(),
	prefersReducedMotion: vi.fn(() => true)
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ toastError }));
vi.mock('$components', () => ({
	logColors: {
		player: '#c0d8ff',
		enemy: '#e8b6a6',
		loot: '#bde0b4',
		effect: '#d0b6ff',
		reward: '#f0d28a',
		system: 'rgba(240,240,240,0.7)'
	},
	// The glyph/row are exercised by their own tests; stub them so this render covers the shell.
	LogRow: (() => {}) as unknown,
	LogGlyph: (() => {}) as unknown
}));
vi.mock('$lib/common', async (importOriginal) => ({
	...(await importOriginal<typeof import('$lib/common')>()),
	prefersReducedMotion
}));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});

import Options from '$routes/game/screens/options/Options.svelte';

const CATEGORY_KEYS = ['logging', 'display', 'audio', 'gameplay', 'account'];

beforeEach(() => {
	mockPlayerManager.logPreferences = [];
});

afterEach(cleanup);

describe('Options screen (settings shell)', () => {
	it('mounts the category rail and the logging panel', () => {
		render(Options);

		expect(screen.getByTestId('options-screen')).toBeTruthy();
		expect(screen.getByTestId('settings-rail')).toBeTruthy();
		expect(screen.getByTestId('logging-panel')).toBeTruthy();
	});

	it('renders a rail item for every settings category', () => {
		render(Options);

		for (const key of CATEGORY_KEYS) {
			expect(screen.getByTestId(`settings-cat-${key}`)).toBeTruthy();
		}
	});

	it('marks Logging active and flags the unbuilt categories as "soon"', () => {
		render(Options);

		expect(screen.getByTestId('settings-cat-logging').classList.contains('active')).toBe(true);
		// Every category except the built Logging one shows a "soon" badge.
		expect(screen.getAllByText('soon')).toHaveLength(CATEGORY_KEYS.length - 1);
	});

	it('keeps the logging panel shown when its rail item is selected', async () => {
		render(Options);

		await fireEvent.click(screen.getByTestId('settings-cat-logging'));

		expect(screen.getByTestId('logging-panel')).toBeTruthy();
	});
});
