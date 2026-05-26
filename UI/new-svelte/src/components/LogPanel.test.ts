import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import LogPanel from './LogPanel.svelte';

vi.mock('$lib/engine', () => ({
	logManager: {
		entries: [],
	},
}));

afterEach(cleanup);

describe('LogPanel', () => {
	it('renders the log panel container', () => {
		render(LogPanel);
		expect(screen.getByTestId('log-panel')).toBeTruthy();
	});

	it('renders the Combat Log header', () => {
		render(LogPanel);
		const panel = screen.getByTestId('log-panel');
		expect(panel.textContent).toContain('Combat Log');
	});
});
