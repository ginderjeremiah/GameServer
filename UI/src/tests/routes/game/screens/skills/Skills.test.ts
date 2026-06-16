import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, fireEvent, screen } from '@testing-library/svelte';
import {
	EAttribute,
	EModifierType,
	ESkillEffectTarget,
	type IChallenge,
	type IEnemy,
	type ISkill,
	type ISkillEffect,
	type IZone
} from '$lib/api';

// Same engine/stores/api mocks as the view-model test: the rendered screen builds
// its own SkillsView from these at mount.
const {
	mockPlayerManager,
	mockInventoryManager,
	sendSocketCommand,
	toastError,
	staticData,
	playerChallenges,
	registerTooltipComponent
} = vi.hoisted(() => {
	const playerManager = {
		unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
		currentZone: 0,
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
		// The rail registers a ChallengeTooltip; the registration is stubbed and the tooltip
		// resolves the gating challenge from this same reference data (completion from the store).
		playerChallenges: { isChallengeCompleted: vi.fn(() => false) },
		registerTooltipComponent: vi.fn(() => ({
			setTooltipPosition: vi.fn(),
			showTooltip: vi.fn(),
			hideTooltip: vi.fn()
		})),
		staticData: {
			skills: [] as ISkill[],
			challenges: [] as IChallenge[],
			zones: undefined as IZone[] | undefined,
			enemies: undefined as IEnemy[] | undefined,
			attributes: undefined as unknown,
			challengeTypes: undefined as unknown,
			items: undefined as unknown,
			itemMods: undefined as unknown
		}
	};
});

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({
	staticData,
	toastError,
	playerChallenges,
	registerTooltipComponent,
	// The gate tooltip content under test is driven by `challengeId`; position is irrelevant here.
	anchorPosition: () => ({ x: 0, y: 0 })
}));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});
// Pin the generated loadout cap so the full-loadout/swap scenarios below stay coherent (the
// fixtures equip three of four unlocked skills); the cap's behavior is what's under test, not its
// value. Partial mock — the other constants must stay real for the transitively-imported engine.
vi.mock('$lib/api/types/game-constants', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, MAX_SELECTED_SKILLS: 3 };
});

import Skills from '$routes/game/screens/skills/Skills.svelte';

const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 10,
	damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }],
	effects: [],
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

// Zone 0 (idle range [2,8], boss enemy 2 at level 10): one in-zone spawn + the boss pill.
const ZONES: IZone[] = [
	{ id: 0, name: 'Vale', description: '', order: 0, levelMin: 2, levelMax: 8, bossEnemyId: 2, bossLevel: 10 }
];

// amountPerLevel 1 keeps the spawn's Defense (2 + 1·level) under this catalogue's slider ceiling.
const enemy = (over: Partial<IEnemy> & { id: number }): IEnemy => ({
	name: `Enemy ${over.id}`,
	isBoss: false,
	attributeDistribution: [{ attributeId: EAttribute.Endurance, baseAmount: 0, amountPerLevel: 1 }],
	skillPool: [],
	spawns: [],
	...over
});

const ENEMIES: IEnemy[] = [
	enemy({ id: 0, name: 'Imp', spawns: [{ zoneId: 0, weight: 1 }] }),
	enemy({ id: 1, name: 'Wolf', spawns: [{ zoneId: 1, weight: 1 }] }),
	enemy({ id: 2, name: 'Ogre King', isBoss: true })
];

const rowByName = (container: HTMLElement, name: string): HTMLElement | undefined =>
	Array.from(container.querySelectorAll<HTMLElement>('.row')).find((r) => r.textContent?.includes(name));

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	toastError.mockReset();
	playerChallenges.isChallengeCompleted.mockReset().mockReturnValue(false);
	staticData.skills = SKILLS;
	staticData.challenges = CHALLENGES;
	staticData.zones = ZONES;
	staticData.enemies = ENEMIES;
	mockPlayerManager.currentZone = 0;
	mockPlayerManager.unlockedSkills = [
		{ skillId: 0, selected: true, order: 0 },
		{ skillId: 1, selected: true, order: 1 },
		{ skillId: 2, selected: true, order: 2 },
		{ skillId: 3, selected: false, order: 0 }
	];
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

	it('keeps a filled card bound to its skill across a reorder (keyed by skill, not slot index)', async () => {
		const { container } = render(Skills);
		// Tag the first card's DOM node so we can recognise it after the reorder.
		const movedCard = container.querySelectorAll<HTMLElement>('.eqcard')[0];
		expect(movedCard.getAttribute('aria-label')).toContain('Alpha');
		movedCard.dataset.identityProbe = 'alpha';

		// Move Alpha (slot 1) to the end → loadout [Bravo, Charlie, Alpha].
		await fireEvent.dragStart(movedCard);
		const cards = container.querySelectorAll<HTMLElement>('.eqcard');
		await fireEvent.dragOver(cards[2]);
		await fireEvent.drop(cards[2]);
		await fireEvent.dragEnd(cards[2]);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2, 0]);

		// Stable keying: the same DOM node follows Alpha to slot 3, rather than the
		// slot-1 node being reused for Bravo (which index keying would do).
		const probed = container.querySelector<HTMLElement>('[data-identity-probe="alpha"]');
		expect(probed).toBe(movedCard);
		expect(probed?.getAttribute('aria-label')).toContain('Alpha');
		const reordered = Array.from(container.querySelectorAll<HTMLElement>('.eqcard'));
		expect(reordered.indexOf(movedCard)).toBe(2);
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

	it('closes the sort/filter modal on Escape', async () => {
		const { container } = render(Skills);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.filt-btn')!);
		expect(container.querySelector('.modal')).toBeTruthy();
		await fireEvent.keyDown(window, { key: 'Escape' });
		expect(container.querySelector('.modal')).toBeNull();
	});

	it('updates effective numbers when the compare-vs defense changes', async () => {
		const { container } = render(Skills);
		const slider = container.querySelector<HTMLInputElement>('input[type="range"]')!;
		await fireEvent.input(slider, { target: { value: '5' } });
		expect(container.querySelector('.vs-defval b')?.textContent).toBe('5');
	});

	it('renders compare-vs enemy pills and snaps the slider when one is clicked', async () => {
		const { container } = render(Skills);
		const pills = Array.from(container.querySelectorAll<HTMLButtonElement>('.epill'));
		// Zone 0's in-zone spawn (Imp) + the boss (Ogre King); Wolf spawns elsewhere.
		expect(pills.map((p) => p.querySelector('.en')?.textContent)).toEqual(['Imp', '◆ Ogre King']);

		await fireEvent.click(pills[0]); // Imp: Defense = 2 + 1*5 = 7
		expect(container.querySelector('.vs-defval b')?.textContent).toBe('7');
		expect(pills[0].classList.contains('on')).toBe(true);

		// Dragging the slider deselects the active pill.
		const slider = container.querySelector<HTMLInputElement>('input[type="range"]')!;
		await fireEvent.input(slider, { target: { value: '3' } });
		expect(pills[0].classList.contains('on')).toBe(false);
		expect(container.querySelector('.vs-defval b')?.textContent).toBe('3');
	});

	it('resorts the available rail when a compare-vs preset is clicked', async () => {
		// Two available skills whose DPS ranking flips when Ogre King's defense (12) is applied.
		// FastLow:  baseDmg=20, cd=1s  → DPS 20 at def=0, DPS  8.0 at def=12 (effective=8)
		// SlowHigh: baseDmg=100, cd=10s → DPS 10 at def=0, DPS  8.8 at def=12 (effective=88)
		staticData.skills = [
			...SKILLS.slice(0, 3),
			skill({ id: 3, name: 'FastLow', baseDamage: 20, damageMultipliers: [], cooldownMs: 1000 }),
			skill({ id: 4, name: 'SlowHigh', baseDamage: 100, damageMultipliers: [], cooldownMs: 10000 })
		];
		mockPlayerManager.unlockedSkills = [
			{ skillId: 0, selected: true, order: 0 },
			{ skillId: 1, selected: true, order: 1 },
			{ skillId: 2, selected: true, order: 2 },
			{ skillId: 3, selected: false, order: 0 },
			{ skillId: 4, selected: false, order: 0 }
		];
		const { container } = render(Skills);

		// `.row` elements: first 3 are equipped (slot-ordered), then available (DPS-ordered).
		const availableRowNames = () =>
			Array.from(container.querySelectorAll<HTMLElement>('.row'))
				.slice(3)
				.map((r) => r.querySelector('.rowname')?.textContent ?? '');

		// Initial DPS order: FastLow (20) > SlowHigh (10) → FastLow first.
		expect(availableRowNames()[0]).toBe('FastLow');
		expect(availableRowNames()[1]).toBe('SlowHigh');

		// Ogre King pill: defense=12. SlowHigh (8.8 DPS) overtakes FastLow (8.0 DPS).
		const ogreKing = Array.from(container.querySelectorAll<HTMLButtonElement>('.epill')).find((p) =>
			p.textContent?.includes('Ogre King')
		)!;
		await fireEvent.click(ogreKing);

		expect(availableRowNames()[0]).toBe('SlowHigh');
		expect(availableRowNames()[1]).toBe('FastLow');
	});

	it('marks effect-bearing skills with a badge and leaves effect-free skills unmarked', () => {
		const effect: ISkillEffect = {
			id: 7,
			target: ESkillEffectTarget.Opponent,
			attributeId: EAttribute.Defense,
			modifierTypeId: EModifierType.Additive,
			amount: -10,
			durationMs: 5000
		};
		staticData.skills = [skill({ id: 0, name: 'Alpha', effects: [effect] }), ...SKILLS.slice(1)];
		const { container } = render(Skills);
		// Alpha (equipped, has effects) shows the badge; Bravo (effect-free) does not.
		expect(rowByName(container, 'Alpha')?.querySelector('.effect-badge')).toBeTruthy();
		expect(rowByName(container, 'Bravo')?.querySelector('.effect-badge')).toBeNull();

		// The badge must anchor to the non-clipped outer tile, not the `overflow: hidden`
		// `.icon-clip`, so its glow isn't cut off (#421).
		const anchor = rowByName(container, 'Alpha')?.querySelector('.effect-badge-anchor');
		expect(anchor?.parentElement?.classList.contains('skill-icon')).toBe(true);
		expect(anchor?.closest('.icon-clip')).toBeNull();
	});

	it('renders an Effects section in the inspector for an effect-bearing skill', async () => {
		const effect: ISkillEffect = {
			id: 7,
			target: ESkillEffectTarget.Opponent,
			attributeId: EAttribute.Defense,
			modifierTypeId: EModifierType.Additive,
			amount: -10,
			durationMs: 5000
		};
		staticData.skills = [skill({ id: 0, name: 'Alpha', effects: [effect] }), ...SKILLS.slice(1)];
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Alpha')!);
		const row = container.querySelector<HTMLElement>('.effect-row');
		expect(row).toBeTruthy();
		expect(row?.querySelector('.emag')?.textContent).toBe('-10');
		expect(row?.querySelector('.eattr')?.textContent).toBe('Defense');
		expect(row?.querySelector('.emeta')?.textContent).toContain('5s');
	});

	it('omits the inspector Effects section for an effect-free skill', async () => {
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Delta')!);
		expect(container.querySelector('.effect-row')).toBeNull();
	});

	it('shows the locked CTA in the inspector for a locked skill', async () => {
		const { container } = render(Skills);
		// Reveal locked skills, then select Echo (locked) into the inspector.
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.filt-btn')!);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.switch')!);
		await fireEvent.click(rowByName(container, 'Echo')!);
		const cta = container.querySelector<HTMLButtonElement>('.d-cta .btn')!;
		expect(cta.disabled).toBe(true);
		expect(cta.textContent).toContain('Locked');
		expect(container.querySelector('.d-hint')?.textContent).toContain('Unlock by completing');
	});

	it('shows the no-scaling note in the damage breakdown for a skill without multipliers', async () => {
		const { container } = render(Skills);
		// Charlie (id 2) has no damage multipliers, so the breakdown reports no scaling.
		await fireEvent.click(rowByName(container, 'Charlie')!);
		expect(screen.getByText('No attribute scaling.')).toBeTruthy();
	});

	it('filters the rail by search term', async () => {
		const { container } = render(Skills);
		const search = container.querySelector<HTMLInputElement>('.search input')!;
		await fireEvent.input(search, { target: { value: 'delta' } });
		expect(container.querySelectorAll('.row').length).toBe(1);
		expect(rowByName(container, 'Delta')).toBeTruthy();
	});

	it('surfaces the gating challenge tooltip when hovering a locked, challenge-gated skill', async () => {
		// Echo (id 4) is locked and is the reward of challenge 0; give that challenge a requirement.
		staticData.challenges = [
			{
				id: 0,
				name: 'Slay Ten',
				description: 'Defeat 10 enemies',
				challengeTypeId: 0,
				progressGoal: 10,
				rewardSkillId: 4
			}
		] as unknown as IChallenge[];
		const { container } = render(Skills);

		// Reveal locked skills so Echo (gated) appears in the rail.
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.filt-btn')!);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.switch')!);
		const echo = rowByName(container, 'Echo')!;

		// Nothing is shown until the locked row is hovered.
		expect(screen.queryByText('Slay Ten')).toBeNull();

		await fireEvent.mouseEnter(echo, { clientX: 5, clientY: 5 });
		// The gating challenge's name + requirement surface; the skill reward stays sealed (incomplete).
		expect(screen.getByText('Slay Ten')).toBeTruthy();
		expect(screen.getByText('Defeat 10 enemies')).toBeTruthy();

		// Leaving the row clears the gate so it no longer renders.
		await fireEvent.mouseLeave(echo);
		expect(screen.queryByText('Slay Ten')).toBeNull();
	});

	it('surfaces the gating challenge tooltip on keyboard focus of a locked, challenge-gated skill', async () => {
		staticData.challenges = [
			{
				id: 0,
				name: 'Slay Ten',
				description: 'Defeat 10 enemies',
				challengeTypeId: 0,
				progressGoal: 10,
				rewardSkillId: 4
			}
		] as unknown as IChallenge[];
		const { container } = render(Skills);

		// Reveal locked skills so Echo (gated) appears in the rail.
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.filt-btn')!);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.switch')!);
		const echo = rowByName(container, 'Echo')!;

		// The gated row is a focusable button; focusing it (keyboard path) surfaces the gate.
		await fireEvent.focus(echo);
		expect(screen.getByText('Slay Ten')).toBeTruthy();
		expect(screen.getByText('Defeat 10 enemies')).toBeTruthy();

		// Blurring the row clears the gate so it no longer renders.
		await fireEvent.blur(echo);
		expect(screen.queryByText('Slay Ten')).toBeNull();
	});

	it('does not surface a challenge tooltip when hovering an unlocked skill', async () => {
		const { container } = render(Skills);
		// Alpha is an unlocked, equipped skill — hovering it must not show any gate.
		await fireEvent.mouseEnter(rowByName(container, 'Alpha')!, { clientX: 5, clientY: 5 });
		expect(screen.queryByText('Slay Ten')).toBeNull();
	});
});
