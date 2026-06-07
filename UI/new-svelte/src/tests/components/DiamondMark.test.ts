// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import DiamondMark from '$components/DiamondMark.svelte';

afterEach(cleanup);

describe('DiamondMark', () => {
	it('renders the diamond container and inner elements', () => {
		const { container } = render(DiamondMark);
		expect(container.querySelector('.diamond-container')).toBeTruthy();
		expect(container.querySelector('.diamond')).toBeTruthy();
		expect(container.querySelector('.diamond-inner')).toBeTruthy();
	});

	it('does not apply pulsing class by default', () => {
		const { container } = render(DiamondMark);
		expect(container.querySelector('.diamond.pulsing')).toBeFalsy();
	});

	it('applies pulsing class when pulsing prop is true', () => {
		const { container } = render(DiamondMark, { props: { pulsing: true } });
		expect(container.querySelector('.diamond.pulsing')).toBeTruthy();
	});

	it('does not apply error class by default', () => {
		const { container } = render(DiamondMark);
		expect(container.querySelector('.diamond.error')).toBeFalsy();
		expect(container.querySelector('.diamond-inner.error')).toBeFalsy();
	});

	it('applies error class to diamond and inner when error prop is true', () => {
		const { container } = render(DiamondMark, { props: { error: true } });
		expect(container.querySelector('.diamond.error')).toBeTruthy();
		expect(container.querySelector('.diamond-inner.error')).toBeTruthy();
	});

	it('applies size style to diamond element', () => {
		const { container } = render(DiamondMark, { props: { size: 18 } });
		const diamond = container.querySelector('.diamond') as HTMLElement;
		expect(diamond.style.width).toBe('18px');
		expect(diamond.style.height).toBe('18px');
	});

	it('defaults to 16px size', () => {
		const { container } = render(DiamondMark);
		const diamond = container.querySelector('.diamond') as HTMLElement;
		expect(diamond.style.width).toBe('16px');
		expect(diamond.style.height).toBe('16px');
	});

	it('applies margin-bottom style when marginBottom prop is provided', () => {
		const { container } = render(DiamondMark, { props: { marginBottom: 36 } });
		const containerEl = container.querySelector('.diamond-container') as HTMLElement;
		expect(containerEl.style.marginBottom).toBe('36px');
	});

	it('does not apply margin-bottom style when marginBottom prop is omitted', () => {
		const { container } = render(DiamondMark);
		const containerEl = container.querySelector('.diamond-container') as HTMLElement;
		expect(containerEl.style.marginBottom).toBe('');
	});
});
