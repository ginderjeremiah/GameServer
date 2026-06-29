import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, within } from '@testing-library/svelte';
import { EAttribute, EAttributeType, EModifierType } from '$lib/api';
import type {
	PathView,
	TierView,
	WordTooltipController
} from '$routes/game/screens/proficiencies/proficiencies-lexicon';

/* WordDetail reads the proficiency/skill/attribute reference data from staticData (skills are id-indexed,
   mirroring ItemTooltip); the rest of $stores stays real (the $components barrel transitively imports the
   engine, which reads them). */
const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));

vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData };
});

import WordDetail from '$routes/game/screens/proficiencies/WordDetail.svelte';

/* ── fixtures ──────────────────────────────────────────────────────────────── */

const attribute = (id: EAttribute, name: string, isPercentage = false) => ({
	id,
	name,
	description: '',
	attributeType: EAttributeType.Secondary,
	isPercentage,
	isHarmful: false,
	code: '',
	displayOrder: 0,
	decimals: 1
});

// Skills are id-indexed (staticData.skills[id]); a sparse array keyed by id matches the live shape.
const SKILLS: { id: number; name: string }[] = [];
for (const s of [
	{ id: 1, name: 'Spark' },
	{ id: 2, name: 'Flame Burst' },
	{ id: 3, name: 'Conflagration' },
	{ id: 4, name: 'Fireball' },
	{ id: 5, name: 'Ember Strike' }
]) {
	SKILLS[s.id] = s;
}

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

const pathView = (o: Partial<PathView> = {}): PathView => ({
	id: 0,
	name: 'Pyromancy',
	word: 'aenkor',
	iconPath: '',
	contributions: [],
	tiers: [],
	...o
});

const stubController = (): WordTooltipController => ({
	describedById: 'tooltip-7',
	show: vi.fn(),
	move: vi.fn(),
	hide: vi.fn()
});

const renderInspector = (tier: TierView, path = pathView(), controller = stubController()) => {
	render(WordDetail, { tier, path, controller });
	return { controller };
};

beforeEach(() => {
	staticData.skills = SKILLS;
	staticData.attributes = [
		attribute(EAttribute.CriticalChance, 'Fire Damage', true),
		attribute(EAttribute.Toughness, 'Toughness')
	];
});

afterEach(() => cleanup());

/* ── header + state pill ───────────────────────────────────────────────────── */

describe('WordDetail — header & state pill', () => {
	it('renders the tier name and its path as the school', () => {
		renderInspector(tierView({ id: 1, name: 'Inferno Magic' }), pathView({ name: 'Pyromancy' }));
		const inspector = screen.getByTestId('word-detail');
		expect(within(inspector).getByText('Inferno Magic')).toBeTruthy();
		expect(within(inspector).getByText('Pyromancy')).toBeTruthy();
	});

	it('shows MASTERED / TRAINING / LEARNING for the maxed / training / unlocked states', () => {
		renderInspector(tierView({ id: 1, state: 'maxed' }));
		expect(screen.getByTestId('state-pill').textContent).toBe('MASTERED');
		cleanup();
		renderInspector(tierView({ id: 2, state: 'training' }));
		expect(screen.getByTestId('state-pill').textContent).toBe('TRAINING');
		cleanup();
		renderInspector(tierView({ id: 3, state: 'unlocked' }));
		expect(screen.getByTestId('state-pill').textContent).toBe('LEARNING');
	});
});

/* ── XP progress ───────────────────────────────────────────────────────────── */

describe('WordDetail — XP progress', () => {
	it('shows the level and residual XP toward the next level', () => {
		renderInspector(tierView({ id: 1, level: 6, maxLevel: 10, xp: 40, xpForNext: 150, state: 'training' }));
		const inspector = screen.getByTestId('word-detail');
		expect(within(inspector).getByText('LV 6 / 10')).toBeTruthy();
		expect(within(inspector).getByText('40 / 150 XP → level 7')).toBeTruthy();
		expect(screen.getByTestId('xp-bar')).toBeTruthy();
	});

	it('reports a maxed tier as fully translated', () => {
		renderInspector(tierView({ id: 1, level: 10, maxLevel: 10, xp: 0, xpForNext: 0, state: 'maxed' }));
		expect(within(screen.getByTestId('word-detail')).getByText('maxed out — fully translated')).toBeTruthy();
	});
});

/* ── decipher reveal (gated by stage) ──────────────────────────────────────── */

describe('WordDetail — decipher reveal', () => {
	it('shows the undeciphered placeholder before the pronunciation is learned', () => {
		renderInspector(tierView({ id: 1, decipher: 'undeciphered' }));
		expect(within(screen.getByTestId('word-detail')).getByText('⟨ undeciphered ⟩')).toBeTruthy();
	});

	it('reveals the pronunciation at the pronunciation stage', () => {
		renderInspector(tierView({ id: 1, decipher: 'pronunciation', pronunciation: 'AYN-kor' }));
		expect(within(screen.getByTestId('word-detail')).getByText('“AYN-kor”')).toBeTruthy();
	});

	it('reveals the translation once translated', () => {
		renderInspector(tierView({ id: 1, decipher: 'translated', translation: 'The First Flame' }));
		expect(within(screen.getByTestId('word-detail')).getByText('The First Flame')).toBeTruthy();
	});

	it('drives the shared decipher tooltip on hover and is described by it', async () => {
		const tier = tierView({ id: 8 });
		const { controller } = renderInspector(tier);
		const block = screen.getByTestId('word-detail').querySelector('.word-block') as HTMLElement;
		expect(block.getAttribute('aria-describedby')).toBe('tooltip-7');
		await fireEvent.mouseEnter(block);
		expect(controller.show).toHaveBeenCalledWith(tier, expect.anything());
		await fireEvent.mouseLeave(block);
		expect(controller.hide).toHaveBeenCalled();
	});
});

/* ── per-level breakdown ladder ────────────────────────────────────────────── */

describe('WordDetail — per-level ladder', () => {
	it('renders one rung per level up to the cap', () => {
		renderInspector(tierView({ id: 1, maxLevel: 10 }));
		expect(within(screen.getByTestId('ladder')).getAllByTestId(/^rung-/)).toHaveLength(10);
	});

	it('formats the per-level bonus and surfaces milestone reward skills', () => {
		const tier = tierView({
			id: 1,
			level: 5,
			maxLevel: 10,
			milestoneLevels: [5],
			levelModifiers: [
				{ level: 1, attributeId: EAttribute.CriticalChance, modifierTypeId: EModifierType.Additive, amount: 0.02 }
			],
			levelRewards: [{ level: 5, rewardSkillId: 2 }]
		});
		renderInspector(tier);
		expect(within(screen.getByTestId('rung-1')).getByText('+2% Fire Damage')).toBeTruthy();
		const milestone = screen.getByTestId('rung-5');
		expect(within(milestone).getByText('grants Flame Burst')).toBeTruthy();
		expect(within(milestone).getByText('★ milestone')).toBeTruthy();
	});

	it('shows a placeholder for a level with no authored payout', () => {
		renderInspector(tierView({ id: 1, maxLevel: 3 }));
		// No modifiers/rewards authored → every rung shows the em-dash placeholder.
		expect(within(screen.getByTestId('rung-2')).getByText('—')).toBeTruthy();
	});
});

/* ── trained-by chips ──────────────────────────────────────────────────────── */

describe('WordDetail — trained-by chips', () => {
	it('renders a chip per distinct contributing skill name', () => {
		const path = pathView({
			contributions: [
				{ skillId: 4, homeTier: 0, weight: 1 },
				{ skillId: 4, homeTier: 1, weight: 0.5 },
				{ skillId: 5, homeTier: 0, weight: 1 }
			]
		});
		renderInspector(tierView({ id: 1 }), path);
		const chips = screen.getByTestId('trained-by');
		expect(within(chips).getByText('Fireball')).toBeTruthy();
		expect(within(chips).getByText('Ember Strike')).toBeTruthy();
		// Deduped — the doubled Fireball contribution yields a single chip.
		expect(within(chips).getAllByText('Fireball')).toHaveLength(1);
	});

	it('shows the empty hint when no skill trains the path', () => {
		renderInspector(tierView({ id: 1 }), pathView({ contributions: [] }));
		expect(within(screen.getByTestId('trained-by')).getByText('— none yet —')).toBeTruthy();
	});
});
