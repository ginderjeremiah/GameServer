// @vitest-environment jsdom
import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { ERarity } from '$lib/api';
import RarityTag from '$components/RarityTag.svelte';

afterEach(cleanup);

describe('RarityTag', () => {
	it('renders the rarity label tinted by the rarity colour', () => {
		const { container, getByText } = render(RarityTag, { props: { rarityId: ERarity.Common } });
		expect(getByText('Common')).toBeTruthy();
		const dot = container.querySelector('.rarity-dot') as HTMLElement;
		const label = container.querySelector('.rarity-label') as HTMLElement;
		expect(dot.style.background).toBe('var(--rarity-common)');
		expect(label.style.color).toBe('var(--rarity-common)');
	});

	it('forwards positioning props through to the root element', () => {
		const { container } = render(RarityTag, {
			props: { rarityId: ERarity.Common, style: 'margin-left: auto' }
		});
		const tag = container.querySelector('.rarity-tag') as HTMLElement;
		expect(tag.style.marginLeft).toBe('auto');
	});
});
