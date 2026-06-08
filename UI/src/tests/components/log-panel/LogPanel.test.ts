import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import { ELogType } from '$lib/api';
import type { LogMessage } from '$lib/engine/log';

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
		logData.unshift({ id: 1, logType: ELogType.Damage, message: 'You used Cleave and dealt 10 damage!' });
		logData.unshift({ id: 2, logType: ELogType.ItemFound, message: 'Unlocked: Iron Helm!' });

		render(LogPanel);
		const panel = screen.getByTestId('log-panel');
		expect(panel.textContent).toContain('Unlocked: Iron Helm!');
		expect(panel.textContent).toContain('You used Cleave and dealt 10 damage!');
		expect(panel.textContent).toContain('2 events');
	});

	it('shows an empty state when there are no logs', () => {
		render(LogPanel);
		expect(screen.getByTestId('log-panel').textContent).toContain('No combat activity yet.');
	});
});
