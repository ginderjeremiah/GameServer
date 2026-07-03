import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { EDamageType, EModifierType, EAttribute, ERarity, ESkillAcquisition } from '$lib/api';
import type { IChallenge, IEnemy, ISignaturePassive, ISkill, IZone } from '$lib/api';
import type { AttributeModifier } from '$lib/battle';
import { classSignaturePassiveModifier } from '$lib/battle/class-modifiers';

// Engine/stores/api are mocked so constructing a real SkillsView doesn't drag in the game engine.
// `inventoryManager.equippedSlots`/`grantedSkillIds` feed the innate (item-granted) skills the band renders.
const { mockPlayerManager, mockInventoryManager, playerProficiencies, sendSocketCommand, toastError, staticData } =
	vi.hoisted(() => {
		const playerManager = {
			unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
			currentZone: 0,
			attributes: [] as { attributeId: number; amount: number }[],
			battleLockedBaseModifiers: [] as AttributeModifier[],
			battleSignaturePassiveModifier: undefined as unknown as (
				resolve: (attribute: EAttribute) => number
			) => AttributeModifier,
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
				equippedSlots: [] as ({ grantedSkillId?: number; name: string } | undefined)[],
				grantedSkillIds: [] as number[]
			},
			playerProficiencies: { battleModifiers: [] as AttributeModifier[] },
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
vi.mock('$stores', () => ({ staticData, toastError, playerProficiencies }));
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
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	designerNotes: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	acquisition: ESkillAcquisition.Item,
	...over
});

const SKILLS: ISkill[] = [
	skill({ id: 0, name: 'Alpha' }),
	skill({ id: 1, name: 'Bravo' }),
	skill({ id: 2, name: 'Charlie' }),
	skill({ id: 3, name: 'Cleave' })
];

// A flat no-op signature passive — the default a player whose class has no passive carries.
const noOpPassive: ISignaturePassive = {
	attributeId: EAttribute.Strength,
	amount: 0,
	scalingAmount: 0,
	modifierType: EModifierType.Additive
};

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
	mockPlayerManager.battleLockedBaseModifiers = [];
	mockPlayerManager.battleSignaturePassiveModifier = (resolve) => classSignaturePassiveModifier(noOpPassive, resolve);
	mockInventoryManager.equipmentStats = [];
	mockInventoryManager.equippedSlots = [];
	mockInventoryManager.grantedSkillIds = [];
	playerProficiencies.battleModifiers = [];
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
		mockInventoryManager.grantedSkillIds = [3];
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
		mockInventoryManager.grantedSkillIds = [0];
		view = new SkillsView();

		const { container } = render(InnateBand, { props: { view } });
		const card = cards(container)[0];
		expect(card.classList.contains('dupe')).toBe(true);
		expect(card.textContent?.toLowerCase()).toContain('already in your loadout');
	});
});
