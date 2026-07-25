import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import TierSpine from '$routes/game/screens/proficiencies/TierSpine.svelte';
import type { PathView, TierView } from '$routes/game/screens/proficiencies/proficiencies-lexicon';
import { pathView as basePathView, stubController, tierView } from './lexicon-test-utils';

// This suite builds a path *from* its spine, so the root tier's word carries up to the path (which is what
// the header renders) — the shared builder takes a plain override object and defaults to an empty spine.
const pathView = (tiers: TierView[], o: Partial<PathView> = {}): PathView =>
	basePathView({ word: tiers[0]?.word ?? '', tiers, ...o });

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

	it('renders the path’s authored description in the header', () => {
		renderSpine(pathView([tierView({ id: 0 })], { description: 'Words that bind flame to will.' }));
		expect(screen.getByTestId('path-description').textContent).toBe('Words that bind flame to will.');
	});

	it('omits the path description entirely when it is empty', () => {
		// The field is `required` but may legitimately be '' — no empty paragraph may be left behind.
		renderSpine(pathView([tierView({ id: 0 })], { description: '' }));
		expect(screen.queryByTestId('path-description')).toBeNull();
	});

	it('draws the spine most-advanced first (root last)', () => {
		// path.tiers is root-first; the spine reverses so the deepest tier renders at the top.
		renderSpine(pathView([tierView({ id: 0 }), tierView({ id: 1 }), tierView({ id: 2 })]));
		const order = [...document.querySelectorAll('[data-testid^="tier-"]')].map((el) => el.getAttribute('data-testid'));
		expect(order).toEqual(['tier-2', 'tier-1', 'tier-0']);
	});
});
