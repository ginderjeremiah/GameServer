import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EChallengeType, EEntityType, EStatisticType, type IChallenge } from '$lib/api';

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		challengeTypes: [] as any[],
		entityOptions: vi.fn(() => [
			{ value: 0, text: 'Cave Bat' },
			{ value: 1, text: 'Catacomb Lich' }
		]),
		entityName: vi.fn(() => 'Cave Bat')
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import ChallengeConditionSection from '$routes/admin/workbench/components/challenge/ChallengeConditionSection.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

const CHALLENGE_TYPES = [
	{
		id: EChallengeType.EnemiesKilled,
		name: 'Enemies Killed',
		goalComparison: 1,
		statisticType: { id: EStatisticType.EnemiesKilled, entityType: EEntityType.Enemy, bossOnly: false }
	},
	{ id: EChallengeType.LevelReached, name: 'Level Reached', goalComparison: 1, statisticType: null }
];

const config = (): EntityConfig<Identified> =>
	({
		key: 'challenges',
		label: 'Challenges',
		singular: 'Challenge',
		glyph: 'box',
		blankName: 'Unnamed',
		newItem: (id: number) => ({ id }),
		meta: () => [],
		sections: [],
		refresh: async () => [],
		persist: async () => []
	}) as unknown as EntityConfig<Identified>;

const setup = (over: Partial<IChallenge> = {}) => {
	const challenge: IChallenge = {
		id: 1,
		name: 'Test',
		description: '',
		challengeTypeId: EChallengeType.EnemiesKilled,
		statisticType: EStatisticType.EnemiesKilled,
		entityType: EEntityType.Enemy,
		targetEntityId: 0,
		progressGoal: 10,
		...over
	} as IChallenge;
	const store = new EntityStore(config(), [challenge as unknown as Identified]);
	const record = store.items[0];
	return { store, record, baseline: store.baselineOf(1) };
};

beforeEach(() => {
	mockReference.challengeTypes = CHALLENGE_TYPES;
	mockReference.entityOptions.mockClear();
	mockReference.entityName.mockClear();
});

afterEach(cleanup);

describe('ChallengeConditionSection', () => {
	it('renders the objective-type select with an option per challenge type', () => {
		const { store, record, baseline } = setup();
		const { container } = render(ChallengeConditionSection, { props: { record, baseline, store } });
		const select = container.querySelector('.type-select select') as HTMLSelectElement;
		expect(select.querySelectorAll('option')).toHaveLength(2);
		expect(select.value).toBe(String(EChallengeType.EnemiesKilled));
	});

	it('renders the plain-language objective preview', () => {
		const { store, record, baseline } = setup();
		render(ChallengeConditionSection, { props: { record, baseline, store } });
		expect(screen.getByText('Players see')).toBeTruthy();
	});

	it('re-derives the tracked statistic and entity when the type changes', async () => {
		const { store, record, baseline } = setup();
		const { container } = render(ChallengeConditionSection, { props: { record, baseline, store } });
		const select = container.querySelector('.type-select select') as HTMLSelectElement;
		await fireEvent.change(select, { target: { value: String(EChallengeType.LevelReached) } });
		const updated = store.items[0] as unknown as IChallenge;
		expect(updated.challengeTypeId).toBe(EChallengeType.LevelReached);
		// Level Reached has no statistic → entity dimension collapses to None and the target clears.
		expect(updated.statisticType).toBeUndefined();
		expect(updated.entityType).toBe(EEntityType.None);
		expect(updated.targetEntityId).toBeUndefined();
	});

	it('flags the type select dirty against a differing baseline', () => {
		const { store } = setup();
		store.patch(1, (d) => ((d as unknown as IChallenge).challengeTypeId = EChallengeType.LevelReached));
		const { container } = render(ChallengeConditionSection, {
			props: { record: store.items[0], baseline: store.baselineOf(1), store }
		});
		expect(container.querySelector('.type-select .dirty-dot')).toBeTruthy();
	});
});
