import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EAttribute, type IChallenge, type ISkill } from '$lib/api';

// Same engine/stores/api mocks as the view-model test: the rendered screen builds
// its own SkillsView from these at mount.
const { mockPlayerManager, mockInventoryManager, sendSocketCommand, toastError, staticData } = vi.hoisted(() => {
	const playerManager = {
		unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
		maxSelectedSkills: 3,
		attributes: [] as { attributeId: number; amount: number }[],
		get selectedSkills(): number[] {
			return playerManager.unlockedSkills
				.filter((s) => s.selected)
				.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
				.map((s) => s.skillId);
		}
	};
	return {
		mockPlayerManager: playerManager,
		mockInventoryManager: { equipmentStats: [] as { attributeId: number; amount: number }[] },
		sendSocketCommand: vi.fn(),
		toastError: vi.fn(),
		staticData: { skills: [] as ISkill[], challenges: [] as IChallenge[], attributes: undefined as unknown }
	};
});

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData, toastError }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});

import Skills from '$routes/game/screens/skills/Skills.svelte';

const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 10,
	damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }],
	description: 'A skill.',
	cooldownMs: 1000,
	iconPath: '',
	...over
});

const SKILLS: ISkill[] = [
	skill({ id: 0, name: 'Alpha' }),
	skill({ id: 1, name: 'Bravo', damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1 }] }),
	skill({ id: 2, name: 'Charlie', damageMultipliers: [] }),
	skill({ id: 3, name: 'Delta' }),
	skill({ id: 4, name: 'Echo', damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1 }] })
];

const CHALLENGES = [
	{ id: 0, name: 'Slay Ten', description: '', challengeTypeId: 0, entityType: 0, progressGoal: 10, rewardSkillId: 4 }
] as unknown as IChallenge[];

const rowByName = (container: HTMLElement, name: string): HTMLElement | undefined =>
	Array.from(container.querySelectorAll<HTMLElement>('.row')).find((r) => r.textContent?.includes(name));

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	toastError.mockReset();
	staticData.skills = SKILLS;
	staticData.challenges = CHALLENGES;
	mockPlayerManager.unlockedSkills = [
		{ skillId: 0, selected: true, order: 0 },
		{ skillId: 1, selected: true, order: 1 },
		{ skillId: 2, selected: true, order: 2 },
		{ skillId: 3, selected: false, order: 0 }
	];
	mockPlayerManager.maxSelectedSkills = 3;
	mockPlayerManager.attributes = [];
	mockInventoryManager.equipmentStats = [];
});

afterEach(() => cleanup());

describe('Skills screen', () => {
	it('renders the header, equipped rows and the inspector', () => {
		const { container } = render(Skills);
		expect(container.querySelector('[data-testid="skills-screen"]')).toBeTruthy();
		expect(container.querySelector('.title')?.textContent).toBe('Skills');
		// 3 equipped rows + 1 available row.
		expect(container.querySelectorAll('.row').length).toBe(4);
		expect(container.querySelectorAll('.eqcard').length).toBe(3);
	});

	it('selects a skill from the rail into the inspector', async () => {
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Delta')!);
		expect(container.querySelector('.d-name')?.textContent).toBe('Delta');
	});

	it('starts and resolves a swap when equipping into a full loadout', async () => {
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Delta')!);
		// Full loadout → the CTA offers a swap.
		const cta = container.querySelector<HTMLButtonElement>('.d-cta .btn')!;
		expect(cta.textContent).toContain('Swap');
		await fireEvent.click(cta);
		// Cards become swap targets; click the first to replace it.
		const target = container.querySelector<HTMLElement>('.eqcard.swap-target')!;
		await fireEvent.click(target);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [3, 1, 2]);
	});

	it('removes a skill from the equipped band', async () => {
		const { container } = render(Skills);
		const remove = container.querySelector<HTMLButtonElement>('.eqcard .rm')!;
		await fireEvent.click(remove);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2]);
		// A now-empty slot is offered; clicking it surfaces an available skill.
		const empty = container.querySelector<HTMLElement>('.eqcard.empty')!;
		await fireEvent.click(empty);
		expect(container.querySelector('.d-name')).toBeTruthy();
	});

	it('reorders the loadout via drag and drop', async () => {
		const { container } = render(Skills);
		const cards = container.querySelectorAll<HTMLElement>('.eqcard');
		await fireEvent.dragStart(cards[0]);
		await fireEvent.dragOver(cards[2]);
		await fireEvent.drop(cards[2]);
		await fireEvent.dragEnd(cards[2]);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2, 0]);
	});

	it('resolves a pending swap from a band card via the keyboard', async () => {
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Delta')!);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.d-cta .btn')!); // start swap
		const target = container.querySelector<HTMLElement>('.eqcard.swap-target')!;
		await fireEvent.keyDown(target, { key: 'Enter' });
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [3, 1, 2]);
	});

	it('drives the sort/filter modal — sort, attribute filter, show-locked, reset', async () => {
		const { container } = render(Skills);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.filt-btn')!);
		expect(container.querySelector('.modal')).toBeTruthy();

		// Show locked → the locked skill (Echo) appears in the rail.
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.switch')!);
		expect(rowByName(container, 'Echo')).toBeTruthy();

		// Sort by name.
		const nameSort = Array.from(container.querySelectorAll<HTMLButtonElement>('.opt')).find(
			(o) => o.textContent?.trim() === 'Name'
		)!;
		await fireEvent.click(nameSort);

		// Filter by an attribute chip.
		const attrChip = container.querySelector<HTMLButtonElement>('.opt.attr')!;
		await fireEvent.click(attrChip);
		expect(attrChip.classList.contains('on')).toBe(true);

		// Reset clears the filters; locked rows hide again.
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.btn.dim')!);
		expect(rowByName(container, 'Echo')).toBeUndefined();

		// Apply closes the modal.
		const apply = Array.from(container.querySelectorAll<HTMLButtonElement>('.mfoot .btn')).find(
			(b) => b.textContent?.trim() === 'Apply'
		)!;
		await fireEvent.click(apply);
		expect(container.querySelector('.modal')).toBeNull();
	});

	it('updates effective numbers when the compare-vs defense changes', async () => {
		const { container } = render(Skills);
		const slider = container.querySelector<HTMLInputElement>('input[type="range"]')!;
		await fireEvent.input(slider, { target: { value: '5' } });
		expect(container.querySelector('.vs-defval b')?.textContent).toBe('5');
	});

	it('filters the rail by search term', async () => {
		const { container } = render(Skills);
		const search = container.querySelector<HTMLInputElement>('.search input')!;
		await fireEvent.input(search, { target: { value: 'delta' } });
		expect(container.querySelectorAll('.row').length).toBe(1);
		expect(rowByName(container, 'Delta')).toBeTruthy();
	});
});
