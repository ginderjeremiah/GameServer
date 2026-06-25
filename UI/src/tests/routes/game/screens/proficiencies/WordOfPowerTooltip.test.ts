import { describe, it, expect, afterEach } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import WordOfPowerTooltip from '$routes/game/screens/proficiencies/WordOfPowerTooltip.svelte';
import type { TierView } from '$routes/game/screens/proficiencies/proficiencies-lexicon';

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

afterEach(() => cleanup());

describe('WordOfPowerTooltip', () => {
	it('renders nothing when no tier is hovered', () => {
		render(WordOfPowerTooltip, { tier: undefined });
		expect(screen.queryByText('Word of Power')).toBeNull();
	});

	it('seals both pronunciation and meaning while undeciphered', () => {
		render(WordOfPowerTooltip, { tier: tierView({ id: 1, decipher: 'undeciphered' }) });
		expect(screen.getByText('Word of Power')).toBeTruthy();
		expect(screen.getAllByText('— sealed —')).toHaveLength(2);
		expect(screen.getByText(/keep training to learn its pronunciation/i)).toBeTruthy();
	});

	it('reveals the pronunciation but seals the meaning at the pronunciation stage', () => {
		render(WordOfPowerTooltip, { tier: tierView({ id: 2, decipher: 'pronunciation', pronunciation: 'AYN-kor' }) });
		expect(screen.getByText('“AYN-kor”')).toBeTruthy();
		expect(screen.getAllByText('— sealed —')).toHaveLength(1);
		expect(screen.getByText(/translate its meaning/i)).toBeTruthy();
	});

	it('reveals both pronunciation and meaning once translated', () => {
		render(WordOfPowerTooltip, {
			tier: tierView({ id: 3, decipher: 'translated', pronunciation: 'AYN-kor', translation: 'The First Flame' })
		});
		expect(screen.getByText('“AYN-kor”')).toBeTruthy();
		expect(screen.getByText('The First Flame')).toBeTruthy();
		expect(screen.queryByText('— sealed —')).toBeNull();
		expect(screen.getByText('Fully deciphered.')).toBeTruthy();
	});
});
