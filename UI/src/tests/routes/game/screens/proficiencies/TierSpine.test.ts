import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import { EActivityKey } from '$lib/api';
import TierSpine from '$routes/game/screens/proficiencies/TierSpine.svelte';
import type {
	PathView,
	TierView,
	WordTooltipController
} from '$routes/game/screens/proficiencies/proficiencies-lexicon';

const tierView = (o: Partial<TierView> & { id: number }): TierView => ({
	name: `Tier ${o.id}`,
	pathOrdinal: o.id,
	level: 0,
	maxLevel: 10,
	xp: 0,
	xpForNext: 100,
	state: 'unlocked',
	frontier: false,
	milestoneLevels: [],
	levelModifiers: [],
	levelRewards: [],
	decipher: 'undeciphered',
	word: `word${o.id}`,
	pronunciation: `pron${o.id}`,
	translation: `means${o.id}`,
	iconPath: '',
	...o
});

const pathView = (tiers: TierView[]): PathView => ({
	id: 0,
	name: 'Pyromancy',
	word: tiers[0]?.word ?? '',
	iconPath: '',
	activityKey: EActivityKey.Physical,
	tiers
});

const stubController = (): WordTooltipController => ({
	describedById: 'tooltip-1',
	show: vi.fn(),
	move: vi.fn(),
	hide: vi.fn()
});

const renderSpine = (path: PathView) =>
	render(TierSpine, { path, selectedTierId: undefined, onSelect: vi.fn(), controller: stubController() });

afterEach(() => cleanup());

describe('TierSpine', () => {
	it('uses the singular word count for a one-tier path', () => {
		renderSpine(pathView([tierView({ id: 0 })]));
		expect(screen.getByText('Pyromancy · 1 WORD KNOWN')).toBeTruthy();
	});

	it('uses the plural word count for a multi-tier path', () => {
		renderSpine(pathView([tierView({ id: 0 }), tierView({ id: 1 })]));
		expect(screen.getByText('Pyromancy · 2 WORDS KNOWN')).toBeTruthy();
	});

	it('draws the spine most-advanced first (root last)', () => {
		// path.tiers is root-first; the spine reverses so the deepest tier renders at the top.
		renderSpine(pathView([tierView({ id: 0 }), tierView({ id: 1 }), tierView({ id: 2 })]));
		const order = [...document.querySelectorAll('[data-testid^="tier-"]')].map((el) => el.getAttribute('data-testid'));
		expect(order).toEqual(['tier-2', 'tier-1', 'tier-0']);
	});
});
