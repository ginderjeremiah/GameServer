import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, within } from '@testing-library/svelte';
import TierCard from '$routes/game/screens/proficiencies/TierCard.svelte';
import type { TierView } from '$routes/game/screens/proficiencies/proficiencies-lexicon';
import type { WordTooltipController } from '$routes/game/screens/proficiencies/word-hover';

const tierView = (o: Partial<TierView> & { id: number }): TierView => ({
	name: `Tier ${o.id}`,
	pathOrdinal: 0,
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

const stubController = (): WordTooltipController => ({
	describedById: 'tooltip-9',
	show: vi.fn(),
	move: vi.fn(),
	hide: vi.fn()
});

const renderCard = (tier: TierView, controller = stubController(), selected = false, last = true) => {
	const onSelect = vi.fn();
	render(TierCard, { tier, selected, last, onSelect, controller });
	return { onSelect, controller };
};

afterEach(() => cleanup());

describe('TierCard', () => {
	it('shows the undeciphered placeholder when the word is undeciphered', () => {
		renderCard(tierView({ id: 1, decipher: 'undeciphered' }));
		expect(within(screen.getByTestId('tier-1')).getByText('⟨ undeciphered ⟩')).toBeTruthy();
	});

	it('reveals the pronunciation at the pronunciation stage', () => {
		renderCard(tierView({ id: 2, decipher: 'pronunciation', pronunciation: 'AYN-kor' }));
		expect(within(screen.getByTestId('tier-2')).getByText('“AYN-kor”')).toBeTruthy();
	});

	it('reveals the translation once translated', () => {
		renderCard(tierView({ id: 3, decipher: 'translated', translation: 'The First Flame' }));
		expect(within(screen.getByTestId('tier-3')).getByText('The First Flame')).toBeTruthy();
	});

	it('shows the training tag only for a training tier', () => {
		renderCard(tierView({ id: 4, state: 'training' }));
		expect(within(screen.getByTestId('tier-4')).queryByText('training')).toBeTruthy();
		cleanup();
		renderCard(tierView({ id: 5, state: 'unlocked' }));
		expect(within(screen.getByTestId('tier-5')).queryByText('training')).toBeNull();
	});

	it('renders the level tag and a pip track sized to the cap', () => {
		renderCard(tierView({ id: 6, level: 3, maxLevel: 10 }));
		const card = screen.getByTestId('tier-6');
		expect(within(card).getByText('3/10')).toBeTruthy();
		expect(within(card).getByLabelText('Level 3 of 10')).toBeTruthy();
	});

	it('selects the tier on click', async () => {
		const { onSelect } = renderCard(tierView({ id: 7 }));
		await fireEvent.click(screen.getByTestId('tier-7'));
		expect(onSelect).toHaveBeenCalledWith(7);
	});

	it('drives the shared tooltip on hover and is described by it', async () => {
		const tier = tierView({ id: 8 });
		const { controller } = renderCard(tier);
		const card = screen.getByTestId('tier-8');
		expect(card.getAttribute('aria-describedby')).toBe('tooltip-9');
		await fireEvent.mouseEnter(card);
		expect(controller.show).toHaveBeenCalledWith(tier, expect.anything());
		await fireEvent.mouseLeave(card);
		expect(controller.hide).toHaveBeenCalled();
	});
});
