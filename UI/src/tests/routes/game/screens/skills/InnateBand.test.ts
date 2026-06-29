import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EDamageType, ERarity, ESkillAcquisition } from '$lib/api';
import type { IChallenge, IEnemy, ISkill, IZone } from '$lib/api';

// Engine/stores/api are mocked so constructing a real SkillsView doesn't drag in the game engine.
// `inventoryManager.equippedSlots` feeds the innate (item-granted) skills the band renders.
const { mockPlayerManager, mockInventoryManager, sendSocketCommand, toastError, staticData } = vi.hoisted(() => {
	const playerManager = {
		unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
		currentZone: 0,
		attributes: [] as { attributeId: number; amount: number }[],
		get selectedSkills(): number[] {
			return playerManager.unlockedSkills
				.filter((s) => s.selected)
				.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
				.map((s) => s.skillId);
		},
		setSelectedSkills() {}
	};
	return {
		mockPlayerManager: playerManager,
		mockInventoryManager: {
			equipmentStats: [] as { attributeId: number; amount: number }[],
			equippedSlots: [] as ({ grantedSkillId?: number; name: string } | undefined)[]
		},
		sendSocketCommand: vi.fn(),
		toastError: vi.fn(),
		staticData: {
			skills: [] as ISkill[],
			challenges: [] as IChallenge[],
			zones: undefined as IZone[] | undefined,
			enemies: undefined as IEnemy[] | undefined,
			attributes: undefined as unknown
		}
	};
});

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData, toastError }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});
vi.mock('$lib/api/types/game-constants', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, MAX_SELECTED_SKILLS: 3 };
});

import { SkillsView } from '$routes/game/screens/skills/skills-view.svelte';
import InnateBand from '$routes/game/screens/skills/InnateBand.svelte';

const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 10,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damageType: EDamageType.Physical,
	acquisition: ESkillAcquisition.Item,
	...over
});

const SKILLS: ISkill[] = [
	skill({ id: 0, name: 'Alpha' }),
	skill({ id: 1, name: 'Bravo' }),
	skill({ id: 2, name: 'Charlie' }),
	skill({ id: 3, name: 'Cleave' })
];

let view: SkillsView;

const cards = (container: HTMLElement) =>
	Array.from(container.querySelectorAll<HTMLElement>('[data-testid="innate-card"]'));

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	staticData.skills = SKILLS;
	// ids 0–2 equipped (the loadout), id 3 unlocked-but-unequipped.
	mockPlayerManager.unlockedSkills = [
		{ skillId: 0, selected: true, order: 0 },
		{ skillId: 1, selected: true, order: 1 },
		{ skillId: 2, selected: true, order: 2 },
		{ skillId: 3, selected: false, order: 0 }
	];
	mockPlayerManager.attributes = [];
	mockInventoryManager.equipmentStats = [];
	mockInventoryManager.equippedSlots = [];
	view = new SkillsView();
});

afterEach(cleanup);

describe('InnateBand', () => {
	it('renders nothing when no equipped item grants a skill', () => {
		const { container } = render(InnateBand, { props: { view } });
		expect(cards(container)).toHaveLength(0);
	});

	it('renders a read-only card per granting item, labelled with its source skill and item', () => {
		mockInventoryManager.equippedSlots = [{ name: 'Staff of Embers', grantedSkillId: 3 }];
		view = new SkillsView();

		const { container } = render(InnateBand, { props: { view } });
		const card = cards(container)[0];
		expect(card).toBeTruthy();
		expect(card.textContent).toContain('Cleave');
		expect(card.textContent).toContain('Staff of Embers');
		// Read-only: no remove/reorder controls (unlike the editable EquippedBand).
		expect(card.querySelector('.rm')).toBeNull();
		expect(card.querySelector('.reorder')).toBeNull();
	});

	it('surfaces a grant that duplicates a selected skill as already in the loadout', () => {
		// Skill 0 (Alpha) is in the equipped loadout, so an item granting it is a duplicate.
		mockInventoryManager.equippedSlots = [{ name: 'Echo Blade', grantedSkillId: 0 }];
		view = new SkillsView();

		const { container } = render(InnateBand, { props: { view } });
		const card = cards(container)[0];
		expect(card.classList.contains('dupe')).toBe(true);
		expect(card.textContent?.toLowerCase()).toContain('already in your loadout');
	});
});
