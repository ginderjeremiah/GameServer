// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import WordOfPower from '$components/WordOfPower.svelte';

afterEach(cleanup);

describe('WordOfPower', () => {
	it('renders the romanization as the DOM text when no label is given', () => {
		const { container } = render(WordOfPower, { props: { text: 'inferno' } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el).toBeTruthy();
		expect(el.textContent?.trim()).toBe('inferno');
	});

	it('exposes no img role or aria-label without a label (text is read directly)', () => {
		const { container } = render(WordOfPower, { props: { text: 'inferno' } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.getAttribute('role')).toBeNull();
		expect(el.getAttribute('aria-label')).toBeNull();
	});

	it('hides the glyphs and announces the label when a label is provided', () => {
		const { container } = render(WordOfPower, { props: { text: 'inferno', label: 'Inferno Magic' } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.getAttribute('role')).toBe('img');
		expect(el.getAttribute('aria-label')).toBe('Inferno Magic');
		const glyphs = el.querySelector('[aria-hidden="true"]') as HTMLElement;
		expect(glyphs).toBeTruthy();
		expect(glyphs.textContent?.trim()).toBe('inferno');
	});

	it('defaults the hover title to the romanization', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether' } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.getAttribute('title')).toBe('aether');
	});

	it('defaults the hover title to the label when one is given', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether', label: 'Aether Path' } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.getAttribute('title')).toBe('Aether Path');
	});

	it('honours an explicit title override', () => {
		const { container } = render(WordOfPower, {
			props: { text: 'aether', label: 'Aether Path', title: 'custom' }
		});
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.getAttribute('title')).toBe('custom');
	});

	it('treats a numeric size as pixels', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether', size: 24 } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.style.fontSize).toBe('24px');
	});

	it('passes a string size through unchanged', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether', size: '1.5rem' } });
		const el = container.querySelector('.word-of-power') as HTMLElement;
		expect(el.style.fontSize).toBe('1.5rem');
	});

	it('does not apply the glow class by default', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether' } });
		expect(container.querySelector('.word-of-power.glow')).toBeFalsy();
	});

	it('applies the glow class when glow is true', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether', glow: true } });
		expect(container.querySelector('.word-of-power.glow')).toBeTruthy();
	});

	it('appends a passed-through class', () => {
		const { container } = render(WordOfPower, { props: { text: 'aether', class: 'extra' } });
		expect(container.querySelector('.word-of-power.extra')).toBeTruthy();
	});
});
