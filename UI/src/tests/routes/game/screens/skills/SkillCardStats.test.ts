import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
import { ERarity, EAttribute, ESkillAcquisition } from '$lib/api';
import type { IChallenge, IEnemy, ISkill, IZone } from '$lib/api';

// Engine/stores/api are mocked so constructing a real SkillsView doesn't drag in the game engine.
// AttributeChip is stubbed to a marker so chip rendering can be asserted without the icon/tooltip machinery.
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
vi.mock('$components/AttributeChip.svelte', () => ({ default: ChipStub }));

import ChipStub from './AttributeChipStub.svelte';
import { SkillsView } from '$routes/game/screens/skills/skills-view.svelte';
import SkillCardStats from '$routes/game/screens/skills/SkillCardStats.svelte';

const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 10,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 2000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Player,
	...over
});

let view: SkillsView;

const setSkills = (skills: ISkill[]) => {
	staticData.skills = skills;
	mockPlayerManager.unlockedSkills = skills.map((s, i) => ({ skillId: s.id, selected: false, order: i }));
	view = new SkillsView();
};

/** Narrows the optional metric to a non-undefined value (failing the test loudly otherwise). */
const metricOf = (id: number) => {
	const metrics = view.metric(id);
	if (!metrics) {
		throw new Error(`expected metrics for skill ${id}`);
	}
	return metrics;
};

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	mockPlayerManager.attributes = [];
	mockInventoryManager.equipmentStats = [];
	mockInventoryManager.equippedSlots = [];
});

afterEach(cleanup);

describe('SkillCardStats', () => {
	it('renders the dmg / cd / dps stat rows from the live view', () => {
		setSkills([skill({ id: 0, baseDamage: 10, cooldownMs: 2000 })]);
		const metrics = metricOf(0);

		const { container } = render(SkillCardStats, { props: { view, metrics } });
		const keys = Array.from(container.querySelectorAll('.stat .k')).map((k) => k.textContent);
		expect(keys).toEqual(['dmg', 'cd', 'dps']);

		const values = Array.from(container.querySelectorAll('.stat .v')).map((v) => v.textContent);
		// dmg = 10 (no defense), cd = 2.0s, dps = 10 / 2 = 5.
		expect(values[0]).toBe('10');
		expect(values[1]).toBe('2.0s');
		expect(values[2]).toBe('5');
	});

	it('renders one chip per damage multiplier', () => {
		setSkills([
			skill({
				id: 0,
				damageMultipliers: [
					{ attributeId: EAttribute.Endurance, multiplier: 2 },
					{ attributeId: EAttribute.Intellect, multiplier: 3 }
				]
			})
		]);
		const metrics = metricOf(0);

		const { container } = render(SkillCardStats, { props: { view, metrics } });
		expect(container.querySelectorAll('[data-testid="attr-chip"]')).toHaveLength(2);
	});

	it('renders no chips when the skill has no damage multipliers', () => {
		setSkills([skill({ id: 0, damageMultipliers: [] })]);
		const metrics = metricOf(0);

		const { container } = render(SkillCardStats, { props: { view, metrics } });
		expect(container.querySelectorAll('[data-testid="attr-chip"]')).toHaveLength(0);
	});
});
