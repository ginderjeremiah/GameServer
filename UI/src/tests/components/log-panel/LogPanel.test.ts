import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { ELogType } from '$lib/api';
import type { LogMessage } from '$lib/engine/log';
import { MIN_LOG_PANEL_HEIGHT, DEFAULT_LOG_PANEL_HEIGHT } from '../../../components/log-panel/log-panel-view.svelte';

const logData: LogMessage[] = [];

vi.mock('$stores', () => ({
	logs: () => logData
}));

import LogPanel from '../../../components/log-panel/LogPanel.svelte';

afterEach(() => {
	cleanup();
	logData.length = 0;
});

describe('LogPanel', () => {
	it('renders the log panel container', () => {
		render(LogPanel);
		expect(screen.getByTestId('log-panel')).toBeTruthy();
	});

	it('renders the Combat Log header', () => {
		render(LogPanel);
		expect(screen.getByTestId('log-panel').textContent).toContain('Combat Log');
	});

	it('renders log messages and the event count', () => {
		// Stored newest-first (unshift), so id 2 is the newest entry.
		logData.unshift({ id: 1, logType: ELogType.Damage, message: 'You used Cleave and dealt 10 damage!', timestamp: 0 });
		logData.unshift({ id: 2, logType: ELogType.ItemFound, message: 'Unlocked: Iron Helm!', timestamp: 0 });

		render(LogPanel);
		const panel = screen.getByTestId('log-panel');
		expect(panel.textContent).toContain('Unlocked: Iron Helm!');
		expect(panel.textContent).toContain('You used Cleave and dealt 10 damage!');
		expect(panel.textContent).toContain('2 events');
	});

	it('renders a skill-effect message with its dedicated glyph', () => {
		logData.unshift({
			id: 3,
			logType: ELogType.SkillEffect,
			message: 'You are empowered: +15 Strength for 5s',
			timestamp: 0
		});

		render(LogPanel);
		expect(screen.getByTestId('log-panel').textContent).toContain('You are empowered: +15 Strength for 5s');
	});

	it('shows an empty state when there are no logs', () => {
		render(LogPanel);
		expect(screen.getByTestId('log-panel').textContent).toContain('No combat activity yet.');
	});

	it('renders a resize handle and applies the panel height inline', () => {
		render(LogPanel);
		const handle = screen.getByTestId('log-resize-handle');
		expect(handle).toBeTruthy();
		expect(handle.getAttribute('role')).toBe('separator');
		// The panel height is driven by an inline style from the resize view-model.
		const panel = screen.getByTestId('log-panel');
		expect(panel.style.height).toMatch(/\d+px/);
	});

	it('exposes the resize handle as a keyboard-operable separator', () => {
		render(LogPanel);
		const handle = screen.getByTestId('log-resize-handle');
		expect(handle.getAttribute('tabindex')).toBe('0');
		expect(handle.getAttribute('aria-orientation')).toBe('horizontal');
		expect(handle.getAttribute('aria-valuenow')).toBe(String(DEFAULT_LOG_PANEL_HEIGHT));
		expect(handle.getAttribute('aria-valuemin')).toBe(String(MIN_LOG_PANEL_HEIGHT));
		// aria-valuemax is populated once the container is measured on mount.
		expect(handle.getAttribute('aria-valuemax')).not.toBeNull();
	});

	it('resizes from the keyboard and reports the new height via aria-valuenow', async () => {
		render(LogPanel);
		const handle = screen.getByTestId('log-resize-handle');
		const before = handle.getAttribute('aria-valuenow');
		// ArrowDown is handled: it cancels the default page scroll and updates the value.
		const notCancelled = await fireEvent.keyDown(handle, { key: 'ArrowDown' });
		expect(notCancelled).toBe(false); // preventDefault was called
		expect(handle.getAttribute('aria-valuenow')).not.toBe(before);
	});

	it('leaves keys it does not handle to their default behaviour', async () => {
		render(LogPanel);
		const handle = screen.getByTestId('log-resize-handle');
		const notCancelled = await fireEvent.keyDown(handle, { key: 'a' });
		expect(notCancelled).toBe(true); // default not prevented
	});
});
