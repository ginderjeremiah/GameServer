import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { createRawSnippet } from 'svelte';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';

afterEach(cleanup);

const header = createRawSnippet(() => ({
	render: () => '<div data-testid="header">header</div>'
}));
const body = createRawSnippet(() => ({
	render: () => '<div data-testid="body-content">body</div>'
}));

describe('TooltipShell', () => {
	it('paints the accent as the panel left border', () => {
		const { container } = render(TooltipShell, { props: { accent: 'var(--rarity-epic)' } });
		const shell = container.querySelector('.tt-shell') as HTMLElement;
		expect(shell.getAttribute('style')).toContain('border-left: 3px solid var(--rarity-epic)');
	});

	it('renders the body content inside the padded .tt-body region', () => {
		const { container } = render(TooltipShell, { props: { accent: 'var(--accent)', children: body } });
		const ttBody = container.querySelector('.tt-body') as HTMLElement;
		expect(ttBody).not.toBeNull();
		expect(ttBody.querySelector('[data-testid="body-content"]')).not.toBeNull();
	});

	it('renders the header flush above the body', () => {
		const { container } = render(TooltipShell, { props: { accent: 'var(--accent)', header, children: body } });
		const shell = container.querySelector('.tt-shell') as HTMLElement;
		// The header is a direct child of the shell, ordered before the body wrapper.
		expect(shell.children[0].getAttribute('data-testid')).toBe('header');
		expect(shell.children[1].classList.contains('tt-body')).toBe(true);
	});

	it('is shown by default (no display:none)', () => {
		const { container } = render(TooltipShell, { props: { accent: 'var(--accent)', children: body } });
		const shell = container.querySelector('.tt-shell') as HTMLElement;
		expect(shell.style.display).not.toBe('none');
		expect(container.querySelector('.tt-body')).not.toBeNull();
	});

	it('hides the panel and renders no contents when hidden', () => {
		const { container } = render(TooltipShell, {
			props: { accent: 'var(--accent)', hidden: true, header, children: body }
		});
		const shell = container.querySelector('.tt-shell') as HTMLElement;
		expect(shell.style.display).toBe('none');
		// A hidden shell is an empty anchored panel — no header, no body wrapper.
		expect(container.querySelector('.tt-body')).toBeNull();
		expect(container.querySelector('[data-testid="header"]')).toBeNull();
		expect(container.querySelector('[data-testid="body-content"]')).toBeNull();
	});
});
