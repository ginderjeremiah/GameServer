import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EAttribute, type IAttribute, type IBattlerAttribute } from '$lib/api';
import { makeAttribute } from '../../../../fixtures/attributes';

const { mockPlayerManager, sendSocketCommand, toastError, staticData } = vi.hoisted(() => ({
	mockPlayerManager: {
		attributes: [] as IBattlerAttribute[],
		statPointsGained: 0,
		statPointsUsed: 0,
		level: 0,
		exp: 0,
		nextLevelThreshold: 0
	},
	sendSocketCommand: vi.fn(),
	toastError: vi.fn(),
	staticData: { attributes: [] as IAttribute[] }
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ staticData, toastError }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});

import Attributes from '$routes/game/screens/attributes/Attributes.svelte';

const refAttributes: IAttribute[] = [
	makeAttribute(EAttribute.Strength, 'Strength'),
	makeAttribute(EAttribute.Endurance, 'Endurance'),
	makeAttribute(EAttribute.Intellect, 'Intellect'),
	makeAttribute(EAttribute.Agility, 'Agility'),
	makeAttribute(EAttribute.Dexterity, 'Dexterity'),
	makeAttribute(EAttribute.Luck, 'Luck'),
	makeAttribute(EAttribute.MaxHealth, 'Max Health'),
	makeAttribute(EAttribute.Toughness, 'Toughness'),
	makeAttribute(EAttribute.CooldownRecovery, 'Cooldown Recovery')
];

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({ data: [] });
	toastError.mockReset();
	localStorage.clear();
	staticData.attributes = refAttributes;
	mockPlayerManager.attributes = [
		{ attributeId: EAttribute.Strength, amount: 5 },
		{ attributeId: EAttribute.Endurance, amount: 5 },
		{ attributeId: EAttribute.Intellect, amount: 5 },
		{ attributeId: EAttribute.Agility, amount: 5 },
		{ attributeId: EAttribute.Dexterity, amount: 5 },
		{ attributeId: EAttribute.Luck, amount: 5 }
	];
	mockPlayerManager.statPointsGained = 8;
	mockPlayerManager.statPointsUsed = 0;
	mockPlayerManager.level = 7;
	mockPlayerManager.exp = 280;
	mockPlayerManager.nextLevelThreshold = 700;
	// The radar reads prefers-reduced-motion to decide whether to animate; force
	// the snap path so no rAF runs during the test.
	window.matchMedia = vi.fn().mockReturnValue({ matches: true }) as unknown as typeof window.matchMedia;
});

afterEach(cleanup);

describe('Attributes screen', () => {
	it('renders the screen with the points budget', () => {
		render(Attributes);
		expect(screen.getByTestId('attributes-screen')).toBeTruthy();
		expect(screen.getByText('Attributes')).toBeTruthy();
		// 8 unspent points available, none pending.
		expect(screen.getByText('8')).toBeTruthy();
		expect(screen.getByText('No changes')).toBeTruthy();
	});

	it('shows the player level and XP progress toward the next level', () => {
		const { getByTestId, container } = render(Attributes);

		const xpBar = getByTestId('attributes-xp-bar');
		expect(xpBar.getAttribute('role')).toBe('progressbar');
		expect(xpBar.getAttribute('aria-valuenow')).toBe('280');
		expect(xpBar.getAttribute('aria-valuemax')).toBe('700');
		// 280 / 700 = 40%.
		expect((xpBar.querySelector('.xp-fill') as HTMLElement).getAttribute('style')).toContain('width: 40%');

		// The visible level and current/total XP readout beside the bar.
		const meter = container.querySelector('.level-meter') as HTMLElement;
		expect(meter.querySelector('.label')?.textContent).toContain('7');
		expect(meter.querySelector('.value')?.textContent).toContain('280');
		expect(meter.querySelector('.value')?.textContent).toContain('700');
	});

	it('spends a point when an attribute stepper is clicked', async () => {
		render(Attributes);
		const addButtons = screen.getAllByLabelText('Add a point', { exact: true });
		expect(addButtons.length).toBe(6);

		await fireEvent.click(addButtons[0]);

		expect(screen.getByText('1 attribute changed')).toBeTruthy();
	});

	it('makes each guided-row attribute icon keyboard-focusable and tied to the shared tooltip', () => {
		const { container } = render(Attributes);
		const hits = container.querySelectorAll('.attr-hit');
		expect(hits.length).toBe(6);
		for (const hit of hits) {
			expect(hit.getAttribute('tabindex')).toBe('0');
			// All rows reference the one shared tooltip container, so the explanation is announced on focus.
			expect(hit.getAttribute('aria-describedby')).toMatch(/^tooltip-\d+$/);
		}
	});

	it('keeps the theory-row attribute icons keyboard-focusable and tooltip-linked', async () => {
		const { container } = render(Attributes);
		await fireEvent.click(screen.getByText('Theorycraft'));
		const hits = container.querySelectorAll('.attr-hit');
		expect(hits.length).toBe(6);
		for (const hit of hits) {
			expect(hit.getAttribute('tabindex')).toBe('0');
			expect(hit.getAttribute('aria-describedby')).toMatch(/^tooltip-\d+$/);
		}
	});

	it('switches into theorycraft mode and reveals the derived-stats panel', async () => {
		render(Attributes);
		expect(screen.queryByText('Derived Stats')).toBeNull();

		await fireEvent.click(screen.getByText('Theorycraft'));

		expect(screen.getByText('Derived Stats')).toBeTruthy();
		expect(screen.getByText('Max Health')).toBeTruthy();
	});

	it('persists the saved allocation deltas when Confirm is clicked', async () => {
		sendSocketCommand.mockResolvedValue({
			data: [
				{ attributeId: EAttribute.Strength, amount: 6 },
				{ attributeId: EAttribute.Endurance, amount: 5 },
				{ attributeId: EAttribute.Intellect, amount: 5 },
				{ attributeId: EAttribute.Agility, amount: 5 },
				{ attributeId: EAttribute.Dexterity, amount: 5 },
				{ attributeId: EAttribute.Luck, amount: 5 }
			]
		});
		render(Attributes);

		await fireEvent.click(screen.getAllByLabelText('Add a point', { exact: true })[0]);
		await fireEvent.click(screen.getByText('Confirm'));

		expect(sendSocketCommand).toHaveBeenCalledWith('UpdatePlayerStats', [
			{ attributeId: EAttribute.Strength, amount: 1 }
		]);
	});
});
