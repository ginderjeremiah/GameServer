import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { flushSync } from 'svelte';
import { ELogType } from '$lib/api';
import { MIN_LOG_PANEL_HEIGHT, DEFAULT_LOG_PANEL_HEIGHT } from '../../../components/log-panel/log-panel-view.svelte';

// The panel reads `logs` from the store barrel; route the barrel to the real logs store so
// tests can drive reactive updates via addLog/resetLogs without pulling in the rest of `$stores`.
vi.mock('$stores', async () => await vi.importActual('$stores/logs.svelte'));

import { addLog, resetLogs } from '$stores';
import LogPanel from '../../../components/log-panel/LogPanel.svelte';

// Mirrors the panel's fixed row height; the nudge arithmetic is asserted against it.
const rowHeight = 30;

// jsdom has no layout, so back scrollTop with a real read/write property the
// pinning effect can add to and the scroll handler can read.
const stubScrollTop = (el: HTMLElement, initial = 0) => {
	let value = initial;
	Object.defineProperty(el, 'scrollTop', {
		configurable: true,
		get: () => value,
		set: (v: number) => {
			value = v;
		}
	});
};

const renderPanel = () => {
	render(LogPanel);
	const body = screen.getByTestId('log-body');
	stubScrollTop(body);
	return body;
};

const scrollTo = async (el: HTMLElement, top: number) => {
	el.scrollTop = top;
	await fireEvent.scroll(el);
};

const addEntries = (count: number, message = 'You used Cleave and dealt 10 damage!') => {
	for (let i = 0; i < count; i++) {
		addLog(ELogType.Damage, message);
	}
	flushSync();
};

const rowClasses = (el: HTMLElement) => [...el.querySelectorAll('.manifest-row')].map((row) => row.classList);

afterEach(() => {
	cleanup();
	resetLogs();
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
		addLog(ELogType.Damage, 'You used Cleave and dealt 10 damage!');
		addLog(ELogType.ItemFound, 'Unlocked: Iron Helm!');

		render(LogPanel);
		const panel = screen.getByTestId('log-panel');
		expect(panel.textContent).toContain('Unlocked: Iron Helm!');
		expect(panel.textContent).toContain('You used Cleave and dealt 10 damage!');
		expect(panel.textContent).toContain('2 events');
	});

	it('renders a skill-effect message with its dedicated glyph', () => {
		addLog(ELogType.SkillEffect, 'You are empowered: +15 Strength for 5s');

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

	describe('entrance animation', () => {
		it('does not animate rows already in the store at first render', () => {
			addLog(ELogType.Damage, 'old entry A');
			addLog(ELogType.Damage, 'old entry B');

			const body = renderPanel();
			for (const classes of rowClasses(body)) {
				expect(classes.contains('animate-in')).toBe(false);
			}
		});

		it('animates every row of a burst arriving while pinned at the top', () => {
			addLog(ELogType.Damage, 'pre-existing');
			const body = renderPanel();

			addEntries(2);
			const classes = rowClasses(body);
			expect(classes).toHaveLength(3);
			expect(classes[0].contains('animate-in')).toBe(true); // newest
			expect(classes[1].contains('animate-in')).toBe(true); // same burst
			expect(classes[2].contains('animate-in')).toBe(false); // pre-existing row
		});

		it('does not animate rows arriving while the reader is scrolled back', async () => {
			addEntries(5);
			const body = renderPanel();
			await scrollTo(body, 100);

			addEntries(1, 'arrived off-screen');
			expect(rowClasses(body)[0].contains('animate-in')).toBe(false);
		});

		it('does not retroactively animate a row when the reader scrolls back to the top', async () => {
			addEntries(5);
			const body = renderPanel();
			await scrollTo(body, 100);
			addEntries(1, 'arrived off-screen');

			// Returning to the top must not re-add the class and replay the entrance.
			await scrollTo(body, 0);
			addEntries(1, 'arrived at top');
			const classes = rowClasses(body);
			expect(classes[0].contains('animate-in')).toBe(true); // new while pinned
			expect(classes[1].contains('animate-in')).toBe(false); // still the off-screen arrival
		});
	});

	describe('scroll pinning', () => {
		it('keeps a top-pinned reader at the top across a multi-entry flush', () => {
			addLog(ELogType.Damage, 'pre-existing');
			const body = renderPanel();

			addEntries(3);
			expect(body.scrollTop).toBe(0);
		});

		it('nudges a back-scrolled reader one row per new entry in a flush', async () => {
			addEntries(5);
			const body = renderPanel();
			await scrollTo(body, 100);

			addEntries(3);
			expect(body.scrollTop).toBe(100 + 3 * rowHeight);
		});

		it('nudges by new-entry count, not list length, once the store cap trims the tail', async () => {
			addEntries(40); // store cap: further adds pop the oldest, keeping length at 40
			const body = renderPanel();
			await scrollTo(body, 100);

			addEntries(2);
			expect(body.scrollTop).toBe(100 + 2 * rowHeight);
		});

		it('clamps the nudge when resetLogs restarts the id counter', async () => {
			addEntries(5);
			const body = renderPanel();
			await scrollTo(body, 100);

			// Ids restart below the last seen id — the delta must clamp to zero, not jump backwards.
			resetLogs();
			addEntries(2);
			expect(body.scrollTop).toBe(100);

			// And the next entry nudges from the fresh baseline again.
			addEntries(1);
			expect(body.scrollTop).toBe(100 + rowHeight);
		});
	});
});
