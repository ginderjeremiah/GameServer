import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EAttribute, EItemModType, ERarity, type IItemMod } from '$lib/api';
import ModTooltip from '$routes/game/screens/challenges/ModTooltip.svelte';

// A prefix mod with a positive and a negative attribute, so the signed/positive/negative
// rendering of the shared TooltipStatsGrid is exercised. `MaxHealth` also covers the
// `normalizeText` split (camelCase -> "Max Health").
const makeMod = (overrides: Partial<IItemMod> = {}): IItemMod => ({
	id: 1,
	name: 'Flaming',
	description: 'Wreathed in flame.',
	itemModTypeId: EItemModType.Prefix,
	rarityId: ERarity.Legendary,
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 5 },
		{ attributeId: EAttribute.MaxHealth, amount: 12 },
		{ attributeId: EAttribute.Defense, amount: -2 }
	],
	tags: [],
	...overrides
});

afterEach(cleanup);

describe('ModTooltip', () => {
	it('accents the tooltip border by the mod rarity', () => {
		const { container } = render(ModTooltip, { props: { mod: makeMod() } });
		const tooltip = container.querySelector('.tt-shell') as HTMLElement;
		expect(tooltip.getAttribute('style')).toContain('var(--rarity-legendary)');
	});

	it('labels and colours the title by the mod type', () => {
		const { container } = render(ModTooltip, { props: { mod: makeMod() } });
		const label = container.querySelector('.tt-category-label') as HTMLElement;
		expect(label.textContent).toBe('Prefix');
		expect(label.getAttribute('style')).toContain('var(--mod-prefix)');
		const name = container.querySelector('.tt-title-name') as HTMLElement;
		expect(name.textContent).toBe('Flaming');
	});

	it('renders each attribute as a normalized, signed effect row', () => {
		const { container } = render(ModTooltip, { props: { mod: makeMod() } });
		const grid = container.querySelector('.tt-stats-grid') as HTMLElement;
		const names = Array.from(grid.querySelectorAll('.tt-stat-name')).map((n) => n.textContent);
		expect(names).toEqual(['Strength', 'Max Health', 'Defense']);

		const values = Array.from(grid.querySelectorAll('.tt-stat-value')) as HTMLElement[];
		expect(values.map((v) => v.textContent?.trim())).toEqual(['+5', '+12', '-2']);
		// Positive amounts are accented positive, negative amounts negative.
		expect(values[0].classList.contains('positive')).toBe(true);
		expect(values[2].classList.contains('negative')).toBe(true);
	});

	it('omits the Effects section when the mod has no attributes', () => {
		const { container } = render(ModTooltip, { props: { mod: makeMod({ attributes: [] }) } });
		expect(container.querySelector('.tt-stats-grid')).toBeNull();
	});

	it('renders the description when present and omits it otherwise', () => {
		const { container, rerender } = render(ModTooltip, { props: { mod: makeMod() } });
		expect((container.querySelector('.tt-description') as HTMLElement).textContent).toBe('Wreathed in flame.');

		rerender({ mod: makeMod({ description: '' }) });
		expect(container.querySelector('.tt-description')).toBeNull();
	});
});
