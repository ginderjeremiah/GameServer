import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import {
	EAttribute,
	EChallengeType,
	EEntityType,
	EModifierType,
	ERarity,
	ESkillAcquisition,
	ESkillEffectTarget,
	EStatisticType
} from '$lib/api';
import { SERVER_STAT_TYPES } from '../stats/stat-fixtures';

// Codex fetches the player's statistics + challenges over the socket and resolves reference data
// from staticData. Mock the socket fetch and replace staticData; keep the real statistics /
// playerChallenges / navigation stores (the screen drives them).
const { mockFetchSocket, staticData } = vi.hoisted(() => ({
	mockFetchSocket: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api')>();
	return { ...actual, fetchSocketData: mockFetchSocket };
});
vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData };
});

import Codex from '$routes/game/screens/codex/Codex.svelte';
import { statistics, playerChallenges, navigation } from '$stores';

const dist = () => [
	{ attributeId: EAttribute.Strength, baseAmount: 10, amountPerLevel: 1 },
	{ attributeId: EAttribute.Endurance, baseAmount: 8, amountPerLevel: 0.8 },
	{ attributeId: EAttribute.Intellect, baseAmount: 3, amountPerLevel: 0.2 },
	{ attributeId: EAttribute.Agility, baseAmount: 6, amountPerLevel: 0.5 },
	{ attributeId: EAttribute.Dexterity, baseAmount: 5, amountPerLevel: 0.4 },
	{ attributeId: EAttribute.Luck, baseAmount: 3, amountPerLevel: 0.2 }
];

const STATS = [
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 0, value: 100 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 1, value: 20 },
	// Per-zone (Emberreach) and per-skill (Cleave) records — the head of each rail/table by default,
	// so they back the "Your record" section in the zone/skill dossiers.
	{ statisticTypeId: EStatisticType.ZonesCleared, entityId: 0, value: 3 },
	{ statisticTypeId: EStatisticType.DamageDealt, entityId: 0, value: 4500 }
];
const PLAYER_CHALLENGES = [{ challengeId: 0, progress: 62, completed: false }];

function seedStaticData(): void {
	staticData.zones = [
		{ id: 0, name: 'Emberreach', order: 1, levelMin: 1, levelMax: 10, bossEnemyId: 2, bossLevel: 10 },
		{ id: 1, name: 'Ashfen Marsh', order: 2, levelMin: 11, levelMax: 22, bossLevel: 22 }
	];
	staticData.enemies = [
		{
			id: 0,
			name: 'Dust Skitterer',
			isBoss: false,
			attributeDistribution: dist(),
			skillPool: [0, 1],
			spawns: [
				{ zoneId: 0, weight: 60 },
				{ zoneId: 1, weight: 20 }
			]
		},
		{
			id: 1,
			name: 'Bog Lurker',
			isBoss: false,
			attributeDistribution: dist(),
			skillPool: [1],
			spawns: [{ zoneId: 1, weight: 40 }]
		},
		{ id: 2, name: 'Cinder Tyrant', isBoss: true, attributeDistribution: dist(), skillPool: [0, 1], spawns: [] }
	];
	staticData.skills = [
		{
			id: 0,
			name: 'Cleave',
			description: 'A wide sweeping strike.',
			baseDamage: 14,
			cooldownMs: 1800,
			acquisition: ESkillAcquisition.Player,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1.5 }],
			effects: []
		},
		{
			id: 1,
			name: 'War Cry',
			description: 'A rallying shout.',
			baseDamage: 0,
			cooldownMs: 6000,
			damageMultipliers: [],
			effects: [
				{
					id: 0,
					target: ESkillEffectTarget.Self,
					attributeId: EAttribute.Strength,
					modifierTypeId: EModifierType.Additive,
					amount: 15,
					durationMs: 5000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				}
			]
		}
	];
	staticData.challenges = [
		{
			id: 0,
			name: 'Cull the Skitterers',
			challengeTypeId: EChallengeType.EnemiesKilled,
			entityType: EEntityType.Enemy,
			targetEntityId: 0,
			progressGoal: 100,
			rewardSkillId: 0
		}
	];
	// War Cry (skill 1) is granted by a Rare staff — the Item acquisition channel.
	staticData.items = [{ id: 0, name: 'Ember Staff', rarityId: ERarity.Rare, grantedSkillId: 1 }];
	staticData.challengeTypes = [{ id: EChallengeType.EnemiesKilled, goalComparison: 1, name: 'Enemies Killed' }];
	staticData.statisticTypes = SERVER_STAT_TYPES;
	staticData.attributes = [
		{ id: EAttribute.Strength, name: 'Strength', code: 'STR', attributeType: 1 },
		{ id: EAttribute.MaxHealth, name: 'Max Health', code: '', attributeType: 2 },
		{ id: EAttribute.Defense, name: 'Defense', code: '', attributeType: 2 }
	];
}

beforeEach(() => {
	seedStaticData();
	statistics.reset();
	playerChallenges.reset();
	navigation.clear();
	navigation.consumePayload();
	mockFetchSocket.mockImplementation((cmd: string) => {
		if (cmd === 'GetPlayerStatistics') {
			return Promise.resolve(STATS);
		}
		if (cmd === 'GetPlayerChallenges') {
			return Promise.resolve(PLAYER_CHALLENGES);
		}
		return Promise.resolve([]);
	});
});

afterEach(() => cleanup());

describe('Codex screen', () => {
	it('renders the tab bar with live counts and the enemy table', async () => {
		render(Codex);
		expect(screen.getByTestId('codex-screen')).toBeTruthy();
		// Three section tabs with real counts.
		expect(screen.getByTestId('codex-tab-enemies').textContent).toContain('3');
		expect(screen.getByTestId('codex-tab-zones').textContent).toContain('2');
		expect(screen.getByTestId('codex-tab-skills').textContent).toContain('2');
		// Rows for every enemy.
		expect(screen.getByTestId('codex-enemy-0')).toBeTruthy();
		expect(screen.getByTestId('codex-enemy-2').textContent).toContain('BOSS');
		// The first enemy's dossier shows by default on the Attributes sub-tab (a normal enemy → slider).
		expect(screen.getByTestId('codex-dossier').textContent).toContain('Dust Skitterer');
		expect(await screen.findByTestId('codex-level-slider')).toBeTruthy();
	});

	it('filters the enemy table by Normal / Boss', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-filter-boss'));
		expect(screen.queryByTestId('codex-enemy-0')).toBeNull();
		expect(screen.getByTestId('codex-enemy-2')).toBeTruthy();
		await fireEvent.click(screen.getByTestId('codex-filter-normal'));
		expect(screen.getByTestId('codex-enemy-0')).toBeTruthy();
		expect(screen.queryByTestId('codex-enemy-2')).toBeNull();
	});

	it('reveals the scaling breakdown when "Show scaling" is toggled', async () => {
		render(Codex);
		expect(screen.queryByText(/\/lvl ×/)).toBeNull();
		await fireEvent.click(screen.getByTestId('codex-scaling-toggle'));
		expect(screen.getAllByText(/\/lvl ×/).length).toBeGreaterThan(0);
	});

	it('shows a fixed-level encounter for a boss', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-enemy-2'));
		expect(screen.getByText('FIXED ENCOUNTER')).toBeTruthy();
		expect(screen.queryByTestId('codex-level-slider')).toBeNull();
	});

	it('navigates the dossier sub-tabs (statistics / skills / spawns / challenges)', async () => {
		render(Codex);
		// Statistics — the player's per-enemy record loads via the socket.
		await fireEvent.click(screen.getByTestId('codex-subtab-statistics'));
		expect(await screen.findByTestId('codex-enemy-stats')).toBeTruthy();
		expect(screen.getByText('Enemies Killed')).toBeTruthy();
		// Skills pool.
		await fireEvent.click(screen.getByTestId('codex-subtab-skills'));
		expect(screen.getByText('Cleave')).toBeTruthy();
		// Spawns table.
		await fireEvent.click(screen.getByTestId('codex-subtab-spawns'));
		expect(screen.getByText('Spawns in 2 zones')).toBeTruthy();
		expect(screen.getByText('Emberreach')).toBeTruthy();
		// Challenges (present for this enemy).
		await fireEvent.click(screen.getByTestId('codex-subtab-challenges'));
		expect(screen.getByText('Cull the Skitterers')).toBeTruthy();
		expect(screen.getByText('62/100')).toBeTruthy();
	});

	it('cross-links an enemy spawn row into the zone dossier', async () => {
		render(Codex);
		// Dust Skitterer's Spawns sub-tab → Emberreach (zone 0).
		await fireEvent.click(screen.getByTestId('codex-subtab-spawns'));
		await fireEvent.click(screen.getByTestId('codex-enemy-spawn-0'));
		// Lands on the Zones tab with Emberreach's dossier open.
		expect(screen.getByTestId('codex-zone-dossier').textContent).toContain('Emberreach');
	});

	it('cross-links an enemy skill row into the skill dossier', async () => {
		render(Codex);
		// Dust Skitterer's Skills sub-tab → Cleave (skill 0).
		await fireEvent.click(screen.getByTestId('codex-subtab-skills'));
		await fireEvent.click(screen.getByTestId('codex-enemy-skill-0'));
		// Lands on the Skills tab with Cleave's dossier open.
		expect(screen.getByTestId('codex-skill-dossier').textContent).toContain('Cleave');
	});

	it('renders the Zones tab as a progression rail + dossier with a boss card and spawn table', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-zones'));
		// A rail row per zone, and the head zone's dossier by default.
		expect(screen.getByTestId('codex-zone-rows')).toBeTruthy();
		expect(screen.getByTestId('codex-zone-0')).toBeTruthy();
		expect(screen.getByTestId('codex-zone-1')).toBeTruthy();
		const dossier = screen.getByTestId('codex-zone-dossier');
		expect(dossier.textContent).toContain('Emberreach');
		expect(dossier.textContent).toContain('1–10'); // level range
		// Emberreach's authored boss + its spawn table.
		expect(screen.getByTestId('codex-zone-boss').textContent).toContain('Cinder Tyrant');
		expect(screen.getByTestId('codex-zone-spawn-0').textContent).toContain('Dust Skitterer');
	});

	it('renders the player’s per-zone record in the zone dossier', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-zones'));
		// Emberreach (head of the rail) has a recorded clear → its "Your record" section renders.
		expect(await screen.findByTestId('codex-zone-stats')).toBeTruthy();
		expect(screen.getByText('Zones Cleared')).toBeTruthy();
	});

	it('cross-links a zone boss card into the enemy dossier', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-zones'));
		await fireEvent.click(screen.getByTestId('codex-zone-boss'));
		// Lands on the Enemies tab with the boss's dossier open.
		expect(screen.getByTestId('codex-dossier').textContent).toContain('Cinder Tyrant');
	});

	it('cross-links a zone spawn row into the enemy dossier', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-zones'));
		await fireEvent.click(screen.getByTestId('codex-zone-spawn-0'));
		expect(screen.getByTestId('codex-dossier').textContent).toContain('Dust Skitterer');
	});

	it('renders the Skills tab as a skill table + dossier with scaling and a used-by cross-link', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-skills'));
		// A row per skill, and the head skill's dossier (Cleave) by default.
		expect(screen.getByTestId('codex-skill-rows')).toBeTruthy();
		expect(screen.getByTestId('codex-skill-0')).toBeTruthy();
		expect(screen.getByTestId('codex-skill-1')).toBeTruthy();
		const dossier = screen.getByTestId('codex-skill-dossier');
		expect(dossier.textContent).toContain('Cleave');
		expect(dossier.textContent).toContain('×1.5'); // Strength scaling chip
		// Cleave is used by the boss → its used-by pill cross-links into the enemy dossier.
		await fireEvent.click(screen.getByTestId('codex-skill-usedby-2'));
		expect(screen.getByTestId('codex-dossier').textContent).toContain('Cinder Tyrant');
	});

	it('renders the player’s per-skill record in the skill dossier', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-skills'));
		// Cleave (head of the table) has recorded damage → its "Your record" section renders.
		expect(await screen.findByTestId('codex-skill-stats')).toBeTruthy();
		expect(screen.getByText('Damage Dealt')).toBeTruthy();
	});

	it('shows how a skill is obtained in its dossier (challenge reward, then item grant)', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-skills'));
		// Cleave (head of the table) is rewarded by a challenge.
		const cleaveDossier = screen.getByTestId('codex-skill-dossier');
		expect(cleaveDossier.textContent).toContain('How to obtain');
		expect(screen.getByTestId('codex-skill-source-challenge-0').textContent).toContain('Cull the Skitterers');
		// War Cry is granted by the Ember Staff item.
		await fireEvent.click(screen.getByTestId('codex-skill-1'));
		expect(screen.getByTestId('codex-skill-source-item-0').textContent).toContain('Ember Staff');
	});

	it('shows a skill’s authored effects in its dossier', async () => {
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-tab-skills'));
		await fireEvent.click(screen.getByTestId('codex-skill-1')); // War Cry — a utility buff
		const dossier = screen.getByTestId('codex-skill-dossier');
		expect(dossier.textContent).toContain('+15');
		expect(dossier.textContent).toContain('Strength');
	});

	it('surfaces a load error in the statistics sub-tab', async () => {
		mockFetchSocket.mockImplementation((cmd: string) =>
			cmd === 'GetPlayerStatistics' ? Promise.reject(new Error('down')) : Promise.resolve([])
		);
		render(Codex);
		await fireEvent.click(screen.getByTestId('codex-subtab-statistics'));
		expect(await screen.findByText('Your statistics could not be loaded.')).toBeTruthy();
	});
});
