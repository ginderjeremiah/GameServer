// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import AttributeIcon from '$components/AttributeIcon.svelte';
import { EAttribute } from '$lib/api';

afterEach(cleanup);

describe('AttributeIcon', () => {
	it('renders an img pointing at the attribute art under /img', () => {
		const { container } = render(AttributeIcon, { props: { id: EAttribute.Strength } });
		const img = container.querySelector('img.attr-icon');
		expect(img).toBeTruthy();
		expect(img?.getAttribute('src')).toBe('/img/Strength.png');
	});

	it('is decorative (empty alt) by default and accepts an override', () => {
		const dflt = render(AttributeIcon, { props: { id: EAttribute.Strength } });
		expect(dflt.container.querySelector('img')?.getAttribute('alt')).toBe('');
		cleanup();
		const labelled = render(AttributeIcon, { props: { id: EAttribute.Strength, alt: 'Strength' } });
		expect(labelled.container.querySelector('img')?.getAttribute('alt')).toBe('Strength');
	});

	it('applies the size prop to width and height (default 16px)', () => {
		const sized = render(AttributeIcon, { props: { id: EAttribute.Toughness, size: 24 } });
		const img = sized.container.querySelector('img') as HTMLElement;
		expect(img.style.width).toBe('24px');
		expect(img.style.height).toBe('24px');
		cleanup();
		const dflt = render(AttributeIcon, { props: { id: EAttribute.Toughness } });
		const dimg = dflt.container.querySelector('img') as HTMLElement;
		expect(dimg.style.width).toBe('16px');
		expect(dimg.style.height).toBe('16px');
	});

	it('renders nothing for an attribute without art (crit/dodge/block, DropBonus)', () => {
		const { container } = render(AttributeIcon, { props: { id: EAttribute.DropBonus } });
		expect(container.querySelector('img')).toBeFalsy();
	});
});
