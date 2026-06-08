import { describe, it, expect, beforeEach } from 'vitest';
import {
	LogPanelView,
	clampLogPanelHeight,
	MIN_LOG_PANEL_HEIGHT,
	DEFAULT_LOG_PANEL_HEIGHT,
	MIN_SCREEN_HEIGHT
} from '../../../components/log-panel/log-panel-view.svelte';

const STORAGE_KEY = 'gameserver.logPanelHeight';

beforeEach(() => {
	localStorage.clear();
});

describe('clampLogPanelHeight', () => {
	it('clamps below the minimum up to the minimum', () => {
		expect(clampLogPanelHeight(10, 500)).toBe(MIN_LOG_PANEL_HEIGHT);
	});

	it('clamps above the maximum down to the maximum', () => {
		expect(clampLogPanelHeight(800, 400)).toBe(400);
	});

	it('passes a value already in range through unchanged', () => {
		expect(clampLogPanelHeight(250, 500)).toBe(250);
	});

	it('floors the maximum at the minimum so a tiny container cannot invert the bounds', () => {
		// max (50) is below the minimum — the minimum wins.
		expect(clampLogPanelHeight(200, 50)).toBe(MIN_LOG_PANEL_HEIGHT);
	});
});

describe('LogPanelView resize gesture', () => {
	let view: LogPanelView;

	beforeEach(() => {
		view = new LogPanelView();
	});

	it('starts at the default height and not dragging', () => {
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT);
		expect(view.dragging).toBe(false);
	});

	it('grows the log when the top edge is dragged up', () => {
		view.beginResize(500, 800);
		expect(view.dragging).toBe(true);
		view.moveResize(440); // 60px up
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT + 60);
	});

	it('shrinks the log when the top edge is dragged down', () => {
		view.beginResize(500, 800);
		view.moveResize(540); // 40px down
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT - 40);
	});

	it('never shrinks below the minimum height', () => {
		view.beginResize(500, 800);
		view.moveResize(900); // far down
		expect(view.height).toBe(MIN_LOG_PANEL_HEIGHT);
	});

	it('never grows past the available container height minus the screen reserve', () => {
		const available = 800;
		view.beginResize(500, available);
		view.moveResize(0); // far up
		expect(view.height).toBe(available - MIN_SCREEN_HEIGHT);
	});

	it('ignores moves when no drag is in progress', () => {
		view.moveResize(100);
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT);
	});

	it('re-clamps the current height down to fit a shrunken container', () => {
		view.beginResize(500, 800);
		view.moveResize(200); // grow to 300px (within the 800-container bounds)
		view.endResize();
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT + 300);

		view.clampToAvailable(400); // now only 240px fits
		expect(view.height).toBe(400 - MIN_SCREEN_HEIGHT);
	});

	it('leaves the height untouched when it still fits the container', () => {
		view.clampToAvailable(800);
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT);
	});
});

describe('LogPanelView persistence', () => {
	it('persists the chosen height on resize end', () => {
		const view = new LogPanelView();
		view.beginResize(500, 800);
		view.moveResize(460); // +40
		view.endResize();
		expect(view.dragging).toBe(false);
		expect(localStorage.getItem(STORAGE_KEY)).toBe(String(DEFAULT_LOG_PANEL_HEIGHT + 40));
	});

	it('does nothing on endResize when not dragging', () => {
		const view = new LogPanelView();
		view.endResize();
		expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
	});

	it('hydrates a previously persisted height, clamped to the minimum', () => {
		localStorage.setItem(STORAGE_KEY, '260');
		const view = new LogPanelView();
		view.hydrate();
		expect(view.height).toBe(260);
	});

	it('clamps a hydrated height to the live container so a large-viewport size cannot overflow', () => {
		localStorage.setItem(STORAGE_KEY, '600');
		const view = new LogPanelView();
		view.hydrate(400); // available − reserve = 240
		expect(view.height).toBe(400 - MIN_SCREEN_HEIGHT);
	});

	it('ignores a stored height below the minimum', () => {
		localStorage.setItem(STORAGE_KEY, '10');
		const view = new LogPanelView();
		view.hydrate();
		expect(view.height).toBe(MIN_LOG_PANEL_HEIGHT);
	});

	it('ignores a malformed stored height', () => {
		localStorage.setItem(STORAGE_KEY, 'not-a-number');
		const view = new LogPanelView();
		view.hydrate();
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT);
	});

	it('keeps the default when nothing is stored', () => {
		const view = new LogPanelView();
		view.hydrate();
		expect(view.height).toBe(DEFAULT_LOG_PANEL_HEIGHT);
	});
});
