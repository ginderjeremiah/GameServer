import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, fireEvent, screen } from '@testing-library/svelte';
import {
	EDamageType,
	ERarity,
	EAttribute,
	EModifierType,
	ESkillAcquisition,
	ESkillEffectTarget,
	type IChallenge,
	type IEnemy,
	type ISkill,
	type ISkillEffect,
	type IZone
} from '$lib/api';

// Same engine/stores/api mocks as the view-model test: the rendered screen builds
// its own SkillsView from these at mount.
const { mockPlayerManager, mockInventoryManager, sendSocketCommand, toastError, staticData, registerTooltipComponent } =
	vi.hoisted(() => {
		const playerManager = {
			unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
			currentZone: 0,
			level: 1,
			attributes: [] as { attributeId: number; amount: number }[],
			get selectedSkills(): number[] {
				return playerManager.unlockedSkills
					.filter((s) => s.selected)
					.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
					.map((s) => s.skillId);
			},
			setSelectedSkills(orderedIds: number[]) {
				for (const unlockedSkill of playerManager.unlockedSkills) {
					const order = orderedIds.indexOf(unlockedSkill.skillId);
					unlockedSkill.selected = order >= 0;
					unlockedSkill.order = order >= 0 ? order : undefined;
				}
			}
		};
		return {
			mockPlayerManager: playerManager,
			mockInventoryManager: {
				equipmentStats: [] as { attributeId: number; amount: number }[],
				equippedSlots: [] as ({ grantedSkillId?: number; name: string } | undefined)[]
			},
			sendSocketCommand: vi.fn(),
			toastError: vi.fn(),
			// The shared attribute tooltip (damage-breakdown chips, equipped band) registers through this;
			// the registration is stubbed since the tooltip wiring itself isn't under test here.
			registerTooltipComponent: vi.fn(() => ({
				describedById: 'tooltip-skill',
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
	registerTooltipComponent,
	// The attribute tooltip computes a position from the hover anchor; the value is irrelevant here.
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
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	acquisition: ESkillAcquisition.Player,
	...over
});

// ids 0–3 are owned (see the unlockedSkills fixture below); Echo (id 4) is unowned, so the screen
// must never list it now that the locked/aspirational catalogue is gone.
const SKILLS: ISkill[] = [
	skill({ id: 0, name: 'Alpha' }),
	skill({ id: 1, name: 'Bravo', damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1 }] }),
	skill({ id: 2, name: 'Charlie', damageMultipliers: [] }),
	skill({ id: 3, name: 'Delta' }),
	skill({ id: 4, name: 'Echo', damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1 }] })
];

// Zone 0 (idle range [2,8], boss enemy 2 at level 10): one in-zone spawn + the boss pill.
const ZONES: IZone[] = [
	{ id: 0, name: 'Vale', description: '', order: 0, levelMin: 2, levelMax: 8, bossEnemyId: 2, bossLevel: 10 }
];

// amountPerLevel 1 gives each enemy Toughness 2·(1·level), within the preset-derived slider ceiling.
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
	staticData.skills = SKILLS;
	staticData.zones = ZONES;
	staticData.enemies = ENEMIES;
	staticData.attributes = undefined;
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
		// Cards become swap targets; activate the first card's overlay button to replace it.
		const target = container.querySelector<HTMLElement>('.eqcard.swap-target .overlay-button')!;
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
		// The overlay button is the drag handle; the card is the drop target.
		const handle = cards[0].querySelector<HTMLElement>('.overlay-button')!;
		await fireEvent.dragStart(handle);
		await fireEvent.dragOver(cards[2]);
		await fireEvent.drop(cards[2]);
		await fireEvent.dragEnd(handle);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2, 0]);
	});

	it('keeps a filled card bound to its skill across a reorder (keyed by skill, not slot index)', async () => {
		const { container } = render(Skills);
		// Tag the first card's DOM node so we can recognise it after the reorder.
		const movedCard = container.querySelectorAll<HTMLElement>('.eqcard')[0];
		expect(movedCard.querySelector('.overlay-button')!.getAttribute('aria-label')).toContain('Alpha');
		movedCard.dataset.identityProbe = 'alpha';

		// Move Alpha (slot 1) to the end → loadout [Bravo, Charlie, Alpha]. The overlay button
		// is the drag handle; the card is the drop target.
		const handle = movedCard.querySelector<HTMLElement>('.overlay-button')!;
		await fireEvent.dragStart(handle);
		const cards = container.querySelectorAll<HTMLElement>('.eqcard');
		await fireEvent.dragOver(cards[2]);
		await fireEvent.drop(cards[2]);
		await fireEvent.dragEnd(handle);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2, 0]);

		// Stable keying: the same DOM node follows Alpha to slot 3, rather than the
		// slot-1 node being reused for Bravo (which index keying would do).
		const probed = container.querySelector<HTMLElement>('[data-identity-probe="alpha"]');
		expect(probed).toBe(movedCard);
		expect(probed?.querySelector('.overlay-button')!.getAttribute('aria-label')).toContain('Alpha');
		const reordered = Array.from(container.querySelectorAll<HTMLElement>('.eqcard'));
		expect(reordered.indexOf(movedCard)).toBe(2);
	});

	it('resolves a pending swap from a band card via the keyboard', async () => {
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Delta')!);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.d-cta .btn')!); // start swap
		// The swap target is a real <button>, so keyboard Enter/Space activation comes for free —
		// it dispatches the same click. jsdom doesn't synthesize that click from a keydown, so fire
		// it directly as the faithful representation of a keyboard activation.
		const target = container.querySelector<HTMLButtonElement>('.eqcard.swap-target .overlay-button')!;
		expect(target.tagName).toBe('BUTTON');
		await fireEvent.click(target);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [3, 1, 2]);
	});

	it('drives the sort/filter modal — sort, attribute filter, reset', async () => {
		const { container } = render(Skills);
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.filt-btn')!);
		expect(container.querySelector('.modal')).toBeTruthy();
		// The locked/aspirational catalogue is gone, so the modal no longer offers a show-locked toggle.
		expect(container.querySelector('.switch')).toBeNull();

		// Sort by name.
		const nameSort = Array.from(container.querySelectorAll<HTMLButtonElement>('.opt')).find(
			(o) => o.textContent?.trim() === 'Name'
		)!;
		await fireEvent.click(nameSort);
		expect(nameSort.classList.contains('on')).toBe(true);

		// Filter by an attribute chip.
		const attrChip = container.querySelector<HTMLButtonElement>('.opt.attr')!;
		await fireEvent.click(attrChip);
		expect(attrChip.classList.contains('on')).toBe(true);

		// Reset clears the active attribute filter.
		await fireEvent.click(container.querySelector<HTMLButtonElement>('.btn.dim')!);
		expect(container.querySelector('.opt.attr')?.classList.contains('on')).toBe(false);

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

		await fireEvent.click(pills[0]); // Imp: Toughness = 2·(1*5) = 10
		expect(container.querySelector('.vs-defval b')?.textContent).toBe('10');
		expect(pills[0].classList.contains('on')).toBe(true);

		// Dragging the slider deselects the active pill.
		const slider = container.querySelector<HTMLInputElement>('input[type="range"]')!;
		await fireEvent.input(slider, { target: { value: '3' } });
		expect(pills[0].classList.contains('on')).toBe(false);
		expect(container.querySelector('.vs-defval b')?.textContent).toBe('3');
	});

	it('preserves the DPS order when a compare-vs preset is clicked (the curve scales uniformly)', async () => {
		// The Toughness curve is one multiplicative factor applied to every hit, so it scales both rows
		// equally and can never reorder them by effective DPS — flat Defense could flip them, the curve can't.
		// FastLow:  baseDmg=20,  cd=1s  → raw DPS 20
		// SlowHigh: baseDmg=100, cd=10s → raw DPS 10
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

		// Ogre King pill: Toughness = 2·(1·10) = 20, halving every hit vs the level-1 player — order unchanged.
		const ogreKing = Array.from(container.querySelectorAll<HTMLButtonElement>('.epill')).find((p) =>
			p.textContent?.includes('Ogre King')
		)!;
		await fireEvent.click(ogreKing);

		expect(container.querySelector('.vs-defval b')?.textContent).toBe('20');
		expect(availableRowNames()[0]).toBe('FastLow');
		expect(availableRowNames()[1]).toBe('SlowHigh');
	});

	it('marks effect-bearing skills with a badge and leaves effect-free skills unmarked', () => {
		const effect: ISkillEffect = {
			id: 7,
			target: ESkillEffectTarget.Opponent,
			attributeId: EAttribute.Toughness,
			modifierTypeId: EModifierType.Additive,
			amount: -10,
			durationMs: 5000,
			scalingAttributeId: EAttribute.Strength,
			scalingAmount: 0
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
			attributeId: EAttribute.Toughness,
			modifierTypeId: EModifierType.Additive,
			amount: -10,
			durationMs: 5000,
			scalingAttributeId: EAttribute.Strength,
			scalingAmount: 0
		};
		staticData.skills = [skill({ id: 0, name: 'Alpha', effects: [effect] }), ...SKILLS.slice(1)];
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Alpha')!);
		const row = container.querySelector<HTMLElement>('.effect-row');
		expect(row).toBeTruthy();
		expect(row?.querySelector('.emag')?.textContent).toBe('-10');
		expect(row?.querySelector('.eattr')?.textContent).toBe('Toughness');
		expect(row?.querySelector('.emeta')?.textContent).toContain('5s');
	});

	it('omits the inspector Effects section for an effect-free skill', async () => {
		const { container } = render(Skills);
		await fireEvent.click(rowByName(container, 'Delta')!);
		expect(container.querySelector('.effect-row')).toBeNull();
	});

	it('folds the expected crit contribution into the inspector breakdown and raw note', async () => {
		// CriticalChance is flagged `isPercentage` so the breakdown's chance renders as `50%`.
		staticData.attributes = [
			{ id: EAttribute.CriticalChance, name: 'Critical Chance', code: 'CRIT', isPercentage: true, decimals: 0 }
		] as unknown as typeof staticData.attributes;
		// Chance 0.5, damage 0.5 + 1.5 base = 2.0 → expected multiplier 1.5; Alpha's raw 10 → +5 crit.
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.CriticalChance, amount: 0.5 },
			{ attributeId: EAttribute.CriticalDamage, amount: 0.5 }
		];
		const { container } = render(Skills);
		// Alpha (equipped slot 1) is selected into the inspector by default.
		expect(container.querySelector('.d-name')?.textContent).toBe('Alpha');

		const crit = container.querySelector('.brk-line.crit') as HTMLElement;
		expect(crit).not.toBeNull();
		expect(crit.textContent).toContain('50%');
		expect(crit.textContent).toContain('×2');
		expect(crit.textContent).toContain('+5');
		// The raw note surfaces the same crit contribution inline.
		expect(container.querySelector('.rawnote')?.textContent).toContain('+5 crit');
	});

	it('shows the no-scaling note in the damage breakdown for a skill without multipliers', async () => {
		const { container } = render(Skills);
		// Charlie (id 2) has no damage multipliers, so the breakdown reports no scaling.
		await fireEvent.click(rowByName(container, 'Charlie')!);
		expect(screen.getByText('No attribute scaling.')).toBeTruthy();
	});

	it('filters the available rail by search term while keeping the equipped loadout visible', async () => {
		const { container } = render(Skills);
		const search = container.querySelector<HTMLInputElement>('.search input')!;
		await fireEvent.input(search, { target: { value: 'delta' } });
		// The 3 equipped rows stay (the equipped rail is never filtered); the available
		// rail narrows to the single matching skill, Delta — 4 rows in total.
		expect(container.querySelectorAll('.row').length).toBe(4);
		expect(rowByName(container, 'Delta')).toBeTruthy();
		expect(rowByName(container, 'Alpha')).toBeTruthy();
	});

	it('lists only the player’s unlocked skills — an unowned skill never appears in the rail', () => {
		// Echo (id 4) is not in the player's unlockedSkills, so the rail must not list it anywhere
		// (the aspirational/locked catalogue has been removed; the Codex answers "how do I get it?").
		const { container } = render(Skills);
		expect(rowByName(container, 'Echo')).toBeUndefined();
		// Only the 3 equipped + the single unlocked-but-unequipped skill (Delta) are shown.
		expect(container.querySelectorAll('.row').length).toBe(4);
	});
});
