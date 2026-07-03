// @vitest-environment jsdom
import { describe, it, expect, afterEach, vi } from 'vitest';
import { focusTrap, FOCUSABLE_SELECTOR } from '$components/focus-trap';

// Each test attaches the action to a node appended to the body; track the teardown + node so the
// window keydown listener and DOM are cleaned up between tests.
let handle: { update(o: unknown): void; destroy(): void } | undefined;
let node: HTMLElement | undefined;

const mount = (html: string, options: Parameters<typeof focusTrap>[1] = {}) => {
	node = document.createElement('div');
	node.innerHTML = html;
	document.body.appendChild(node);
	handle = focusTrap(node, options);
	return node;
};

const tab = (shiftKey = false) => window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', shiftKey }));

afterEach(() => {
	handle?.destroy();
	handle = undefined;
	node?.remove();
	node = undefined;
	document.body.classList.remove('test-lock');
});

describe('focusTrap', () => {
	it('wraps Tab between the first and last focusables', () => {
		const el = mount('<button data-testid="a">a</button><button data-testid="b">b</button>');
		const [a, b] = [...el.querySelectorAll('button')];

		b.focus();
		tab();
		expect(document.activeElement).toBe(a);

		a.focus();
		tab(true);
		expect(document.activeElement).toBe(b);
	});

	it('pulls focus back inside when it has escaped the node', () => {
		const el = mount('<button>a</button>');
		const a = el.querySelector('button') as HTMLElement;
		document.body.focus();
		tab();
		expect(document.activeElement).toBe(a);
	});

	it('traps non-button focusables too (links, inputs, [tabindex])', () => {
		// The latent bug fixed here: a button-only selector would not have trapped these.
		const el = mount('<a href="#" data-testid="link">link</a><input data-testid="field" /><span tabindex="0">x</span>');
		const link = el.querySelector('a') as HTMLElement;
		const span = el.querySelector('span') as HTMLElement;

		span.focus();
		tab();
		expect(document.activeElement).toBe(link);

		link.focus();
		tab(true);
		expect(document.activeElement).toBe(span);
	});

	it('ignores a disabled button and a tabindex="-1" element as focusables', () => {
		const el = mount(
			'<button data-testid="a">a</button><button disabled>skip</button><div tabindex="-1">skip</div><button data-testid="b">b</button>'
		);
		const [a, b] = el.querySelectorAll<HTMLElement>('[data-testid]');
		b.focus();
		tab();
		expect(document.activeElement).toBe(a);
	});

	it('routes Escape to onEscape', () => {
		const onEscape = vi.fn();
		mount('<button>a</button>', { onEscape });
		window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
		expect(onEscape).toHaveBeenCalledTimes(1);
	});

	it('locks body scroll via the given class and releases it on destroy', () => {
		mount('<button>a</button>', { scrollLockClass: 'test-lock' });
		expect(document.body.classList.contains('test-lock')).toBe(true);
		handle?.destroy();
		handle = undefined;
		expect(document.body.classList.contains('test-lock')).toBe(false);
	});

	it('restores focus to the previously focused element on destroy', () => {
		const outside = document.createElement('button');
		document.body.appendChild(outside);
		outside.focus();
		expect(document.activeElement).toBe(outside);

		const el = mount('<button>a</button>');
		(el.querySelector('button') as HTMLElement).focus();
		expect(document.activeElement).not.toBe(outside);

		handle?.destroy();
		handle = undefined;
		expect(document.activeElement).toBe(outside);
		outside.remove();
	});

	it('does nothing on Tab when the node has no focusables', () => {
		mount('<span>nothing</span>');
		// No throw, and focus is untouched.
		expect(() => tab()).not.toThrow();
	});

	it('exposes a focusable selector that covers the standard interactive elements', () => {
		expect(FOCUSABLE_SELECTOR).toContain('a[href]');
		expect(FOCUSABLE_SELECTOR).toContain('button:not([disabled])');
		expect(FOCUSABLE_SELECTOR).toContain('input:not([disabled])');
		expect(FOCUSABLE_SELECTOR).toContain('[tabindex]:not([tabindex="-1"])');
	});

	it('routes keydown to only the top-most trap while stacked, resuming the outer when the inner unmounts', () => {
		const outerEscape = vi.fn();
		const innerEscape = vi.fn();
		const outer = mount('<button data-testid="o">o</button>', { onEscape: outerEscape });

		// A second trap layered above the first (e.g. a confirm modal over the trapped item drawer).
		const innerNode = document.createElement('div');
		innerNode.innerHTML = '<button data-testid="i">i</button>';
		document.body.appendChild(innerNode);
		const innerHandle = focusTrap(innerNode, { onEscape: innerEscape });

		// Escape dismisses the inner overlay only — no double-dismiss of the one beneath.
		window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
		expect(innerEscape).toHaveBeenCalledTimes(1);
		expect(outerEscape).not.toHaveBeenCalled();

		// Tab is pulled into the inner trap; the outer trap doesn't fight it for focus.
		(outer.querySelector('button') as HTMLElement).focus();
		tab();
		expect(document.activeElement).toBe(innerNode.querySelector('button'));

		innerHandle.destroy();
		innerNode.remove();

		// With the inner trap gone, the outer trap owns Escape again.
		window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape' }));
		expect(outerEscape).toHaveBeenCalledTimes(1);
		expect(innerEscape).toHaveBeenCalledTimes(1);
	});
});
