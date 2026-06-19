import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import type { IChallenge, IEnemy, ISkill, IZone } from '$lib/api';

// Engine/stores/api are mocked so constructing a real SkillsView doesn't drag in the game engine.
// `playerManager.selectedSkills` derives the equipped order from `unlockedSkills`, mirroring the
// real PlayerManager; the band reads everything else off the view.
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
		mockInventoryManager: { equipmentStats: [] as { attributeId: number; amount: number }[] },
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
// Pin the loadout cap so the full-loadout/swap scenarios stay coherent (three equipped of four
// unlocked). Partial mock — the other constants must stay real for the transitively-imported engine.
vi.mock('$lib/api/types/game-constants', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, MAX_SELECTED_SKILLS: 3 };
});

import { SkillsView } from '$routes/game/screens/skills/skills-view.svelte';
import EquippedBand from '$routes/game/screens/skills/EquippedBand.svelte';

const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 10,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	...over
});

const SKILLS: ISkill[] = [
	skill({ id: 0, name: 'Alpha' }),
	skill({ id: 1, name: 'Bravo' }),
	skill({ id: 2, name: 'Charlie' }),
	skill({ id: 3, name: 'Delta' })
];

let view: SkillsView;

const filledCards = (container: HTMLElement) =>
	Array.from(container.querySelectorAll<HTMLElement>('.eqcard:not(.empty)'));

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	toastError.mockReset();
	staticData.skills = SKILLS;
	// ids 0–3 unlocked; 0–2 equipped in order (a full loadout, cap 3); 3 available.
	mockPlayerManager.unlockedSkills = [
		{ skillId: 0, selected: true, order: 0 },
		{ skillId: 1, selected: true, order: 1 },
		{ skillId: 2, selected: true, order: 2 },
		{ skillId: 3, selected: false, order: 0 }
	];
	mockPlayerManager.attributes = [];
	mockInventoryManager.equipmentStats = [];
	view = new SkillsView();
});

afterEach(cleanup);

describe('EquippedBand — accessible card structure', () => {
	it('exposes each filled card as a presentational container with a real <button> overlay', () => {
		const { container } = render(EquippedBand, { props: { view } });
		const cards = filledCards(container);
		expect(cards).toHaveLength(3);
		for (const card of cards) {
			const overlay = card.querySelector('.overlay-button');
			expect(overlay?.tagName).toBe('BUTTON');
			// The card itself is presentational — no hand-rolled button semantics or focus.
			expect(card.getAttribute('role')).toBeNull();
			expect(card.getAttribute('tabindex')).toBeNull();
			expect(card.getAttribute('onkeydown')).toBeNull();
		}
	});

	it('labels each card overlay with the skill name and loadout slot', () => {
		const { container } = render(EquippedBand, { props: { view } });
		const labels = filledCards(container).map((c) => c.querySelector('.overlay-button')!.getAttribute('aria-label'));
		expect(labels).toEqual(['Alpha, loadout slot 1', 'Bravo, loadout slot 2', 'Charlie, loadout slot 3']);
	});

	it('keeps the remove button a labelled sibling of the overlay, not nested inside it', () => {
		const { container } = render(EquippedBand, { props: { view } });
		const card = filledCards(container)[0];
		const remove = card.querySelector('.rm');
		expect(remove?.tagName).toBe('BUTTON');
		expect(remove!.getAttribute('aria-label')).toBe('Remove Alpha from loadout');
		// Nesting a <button> inside the overlay <button> would be invalid HTML.
		expect(card.querySelector('.overlay-button .rm')).toBeNull();
	});

	it('makes the overlay the drag handle when not swapping', () => {
		const { container } = render(EquippedBand, { props: { view } });
		expect(filledCards(container)[0].querySelector('.overlay-button')!.getAttribute('draggable')).toBe('true');
	});
});

// The primary action is a real <button>, so pointer and keyboard share one activation path:
// pressing Enter/Space on the focused button dispatches a click. jsdom doesn't synthesize that
// click from a keydown, so these fire the click directly — the faithful representation of both.
describe('EquippedBand — activation (pointer + keyboard share one path)', () => {
	it('inspects the skill when a card is activated', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(filledCards(container)[1].querySelector('.overlay-button')!);
		expect(view.selectedId).toBe(1); // Bravo, slot 2
	});

	it('removes the skill from the loadout when the remove button is activated', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(filledCards(container)[0].querySelector('.rm')!);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2]);
		expect(view.equipped).toEqual([1, 2]);
	});
});

describe('EquippedBand — swap flow', () => {
	beforeEach(() => {
		// Equipping into a full loadout starts a swap awaiting the slot to replace.
		view.toggle(3);
		expect(view.pendingSwap).toBe(3);
	});

	it('marks cards as swap targets and drops the drag handle while a swap is pending', () => {
		const { container } = render(EquippedBand, { props: { view } });
		for (const card of filledCards(container)) {
			expect(card.classList.contains('swap-target')).toBe(true);
			expect(card.querySelector('.overlay-button')!.getAttribute('draggable')).not.toBe('true');
			// The remove button is replaced by the "replace" hint mid-swap.
			expect(card.querySelector('.rm')).toBeNull();
			expect(card.querySelector('.rep')).toBeTruthy();
		}
	});

	it('resolves the pending swap into the activated card slot', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(filledCards(container)[1].querySelector('.overlay-button')!);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [0, 3, 2]);
		expect(view.equipped).toEqual([0, 3, 2]);
		expect(view.pendingSwap).toBeNull();
	});

	it('cancels the swap from the band head', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(container.querySelector('.band-chip.action')!);
		expect(view.pendingSwap).toBeNull();
		expect(filledCards(container)[0].classList.contains('swap-target')).toBe(false);
	});
});

describe('EquippedBand — drag to reorder', () => {
	it('reorders the loadout when a card is dragged onto another', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		const cards = filledCards(container);
		const handle = cards[0].querySelector('.overlay-button')!;
		await fireEvent.dragStart(handle, { dataTransfer: { setData: vi.fn(), effectAllowed: '' } });
		await fireEvent.dragOver(cards[2]);
		await fireEvent.drop(cards[2]);
		await fireEvent.dragEnd(handle);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2, 0]);
		expect(view.equipped).toEqual([1, 2, 0]);
	});

	it('highlights the dragged card and the hovered drop target', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		const cards = filledCards(container);
		await fireEvent.dragStart(cards[0].querySelector('.overlay-button')!, {
			dataTransfer: { setData: vi.fn(), effectAllowed: '' }
		});
		await fireEvent.dragOver(cards[1]);
		expect(cards[0].classList.contains('dragging')).toBe(true);
		expect(cards[1].classList.contains('dragover')).toBe(true);
	});
});

// HTML5 drag-and-drop is mouse-only, so the move buttons give keyboard and touch users a real,
// focusable path to change battle priority. The buttons are native <button>s, so a click here
// faithfully represents both a pointer/touch tap and a keyboard activation.
describe('EquippedBand — reorder via move buttons (keyboard/touch)', () => {
	const moveButtons = (card: HTMLElement) => Array.from(card.querySelectorAll<HTMLButtonElement>('.reorder .mv'));

	it('renders move-earlier and move-later buttons on each filled card, labelled for assistive tech', () => {
		const { container } = render(EquippedBand, { props: { view } });
		const [earlier, later] = moveButtons(filledCards(container)[1]);
		expect(earlier.tagName).toBe('BUTTON');
		expect(earlier.getAttribute('aria-label')).toBe('Move Bravo earlier in priority');
		expect(later.getAttribute('aria-label')).toBe('Move Bravo later in priority');
	});

	it('disables move-earlier on the first slot and move-later on the last slot', () => {
		const { container } = render(EquippedBand, { props: { view } });
		const cards = filledCards(container);
		expect(moveButtons(cards[0])[0].disabled).toBe(true); // first slot can't move earlier
		expect(moveButtons(cards[0])[1].disabled).toBe(false);
		expect(moveButtons(cards[2])[0].disabled).toBe(false);
		expect(moveButtons(cards[2])[1].disabled).toBe(true); // last slot can't move later
	});

	it('moves a skill later in priority when its move-later button is activated', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(moveButtons(filledCards(container)[0])[1]);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 0, 2]);
		expect(view.equipped).toEqual([1, 0, 2]);
	});

	it('moves a skill earlier in priority when its move-earlier button is activated', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(moveButtons(filledCards(container)[2])[0]);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [0, 2, 1]);
		expect(view.equipped).toEqual([0, 2, 1]);
	});

	it('hides the move buttons while a swap is pending', () => {
		view.toggle(3); // full loadout → starts a swap
		const { container } = render(EquippedBand, { props: { view } });
		expect(container.querySelector('.reorder')).toBeNull();
	});
});

describe('EquippedBand — empty slot', () => {
	beforeEach(() => {
		// Free a slot so the band renders one empty card.
		view.equipped = [0, 1];
	});

	it('renders the empty slot as a real focusable button, not a presentational tile', () => {
		const { container } = render(EquippedBand, { props: { view } });
		const empty = container.querySelector('.eqcard.empty');
		expect(empty?.tagName).toBe('BUTTON');
	});

	it('surfaces the first available skill to equip when the empty slot is activated', async () => {
		const { container } = render(EquippedBand, { props: { view } });
		await fireEvent.click(container.querySelector('.eqcard.empty')!);
		// Available rail is [Charlie(2), Delta(3)]; the first is surfaced into the inspector.
		expect(view.selectedId).toBe(2);
	});
});
