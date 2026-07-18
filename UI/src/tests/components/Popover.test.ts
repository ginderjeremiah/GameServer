// @vitest-environment jsdom
import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { createRawSnippet, tick } from 'svelte';
import Popover from '$components/Popover.svelte';

afterEach(() => {
	cleanup();
	document.body.classList.remove('popover-open');
});

// Two focusable buttons so the focus-trap edges can be exercised.
const children = createRawSnippet(() => ({
	render: () => '<div><button data-testid="first">first</button><button data-testid="last">last</button></div>'
}));

const setup = (open: boolean, onClose = vi.fn()) => {
	const result = render(Popover, { props: { open, onClose, label: 'Filters', children } });
	return { ...result, onClose };
};

describe('Popover', () => {
	it('renders nothing while closed and the dialog chrome once open', async () => {
		const { container, rerender } = setup(false);
		expect(container.querySelector('.popover-shell')).toBeNull();

		await rerender({ open: true, onClose: vi.fn(), label: 'Filters', children });
		const shell = container.querySelector('.popover-shell') as HTMLElement;
		expect(shell).not.toBeNull();
		expect(shell.getAttribute('role')).toBe('dialog');
		expect(shell.getAttribute('aria-label')).toBe('Filters');
		expect(container.querySelector('[data-testid="first"]')).not.toBeNull();
	});

	it('closes on Escape and on a backdrop click', async () => {
		const { container, onClose } = setup(true);
		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(onClose).toHaveBeenCalledTimes(1);

		await fireEvent.click(container.querySelector('.popover-backdrop')!);
		expect(onClose).toHaveBeenCalledTimes(2);
	});

	it('ignores Escape while closed', async () => {
		const { onClose } = setup(false);
		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(onClose).not.toHaveBeenCalled();
	});

	it('moves focus into the popover on open and traps Tab within it', async () => {
		const { getByTestId } = setup(true);
		await tick();
		const first = getByTestId('first');
		const last = getByTestId('last');
		// Opening focuses the first focusable inside the shell.
		expect(document.activeElement).toBe(first);

		// Tab off the last element wraps back to the first.
		last.focus();
		await fireEvent.keyDown(window, { key: 'Tab' });
		expect(document.activeElement).toBe(first);

		// Shift+Tab off the first element wraps to the last.
		first.focus();
		await fireEvent.keyDown(window, { key: 'Tab', shiftKey: true });
		expect(document.activeElement).toBe(last);
	});

	it('restores focus to the prior element when it closes', async () => {
		const outside = document.createElement('button');
		document.body.appendChild(outside);
		outside.focus();
		expect(document.activeElement).toBe(outside);

		const { rerender } = setup(false);
		await rerender({ open: true, onClose: vi.fn(), label: 'Filters', children });
		await tick();
		expect(document.activeElement).not.toBe(outside);

		await rerender({ open: false, onClose: vi.fn(), label: 'Filters', children });
		await tick();
		expect(document.activeElement).toBe(outside);
		outside.remove();
	});

	it('re-anchors focus onto the new content when the focused element unmounts (async loading -> ready swap)', async () => {
		const loadingChildren = createRawSnippet(() => ({
			render: () => '<div><button data-testid="cancel">cancel</button></div>'
		}));
		const readyChildren = createRawSnippet(() => ({
			render: () => '<div><button data-testid="pick">pick</button></div>'
		}));

		const { getByTestId, rerender } = render(Popover, {
			props: { open: true, onClose: vi.fn(), label: 'Switcher', children: loadingChildren }
		});
		await tick();
		expect(document.activeElement).toBe(getByTestId('cancel'));

		await rerender({ open: true, onClose: vi.fn(), label: 'Switcher', children: readyChildren });
		await tick();
		expect(document.activeElement).toBe(getByTestId('pick'));
	});

	it('locks body scroll while open and releases it on close', async () => {
		const { rerender } = setup(true);
		await tick();
		expect(document.body.classList.contains('popover-open')).toBe(true);

		await rerender({ open: false, onClose: vi.fn(), label: 'Filters', children });
		await tick();
		expect(document.body.classList.contains('popover-open')).toBe(false);
	});
});
