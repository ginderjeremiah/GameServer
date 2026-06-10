import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, fireEvent } from '@testing-library/svelte';
import { EChallengeGoalComparison, EChallengeType, EStatisticType, type IChallenge } from '$lib/api';

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		challengeTypes: [] as { id: number; goalComparison?: number }[]
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import GoalField from '$routes/admin/workbench/components/challenge/GoalField.svelte';
import { EntityStore } from '$routes/admin/workbench/entity-store.svelte';
import type { EntityConfig, Identified } from '$routes/admin/workbench/entities/types';

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

const setup = (over: Partial<IChallenge>) => {
	const challenge: IChallenge = {
		id: 1,
		name: 'Test',
		description: '',
		challengeTypeId: EChallengeType.EnemiesKilled,
		progressGoal: 25,
		...over
	} as IChallenge;
	const store = new EntityStore(config(), [challenge as unknown as Identified]);
	const record = store.items[0] as unknown as IChallenge;
	return { store, challenge: record, baseline: store.baselineOf(1) as unknown as IChallenge };
};

beforeEach(() => {
	mockReference.challengeTypes = [];
});

afterEach(cleanup);

describe('GoalField', () => {
	it('labels an accumulating count goal with the statistic unit', () => {
		const { store, challenge, baseline } = setup({ statisticType: EStatisticType.EnemiesKilled });
		const { container } = render(GoalField, { props: { challenge, baseline, store } });
		expect((container.querySelector('.lbl') as HTMLElement).textContent).toContain('Goal amount');
		expect((container.querySelector('.suffix') as HTMLElement).textContent).toBe('kills');
	});

	it('labels a time goal with an "at most" hint and ms unit', () => {
		mockReference.challengeTypes = [{ id: EChallengeType.TimeTrial, goalComparison: EChallengeGoalComparison.AtMost }];
		const { store, challenge, baseline } = setup({
			challengeTypeId: EChallengeType.TimeTrial,
			statisticType: EStatisticType.FastestVictory
		});
		const { container } = render(GoalField, { props: { challenge, baseline, store } });
		const label = (container.querySelector('.lbl') as HTMLElement).textContent ?? '';
		expect(label).toContain('Goal (time)');
		expect(label).toContain('at most');
		expect((container.querySelector('.suffix') as HTMLElement).textContent).toBe('ms');
	});

	it('marks dirty against a differing baseline', () => {
		const { store, challenge } = setup({ statisticType: EStatisticType.EnemiesKilled });
		const baseline = { ...challenge, progressGoal: 99 } as IChallenge;
		const { container } = render(GoalField, { props: { challenge, baseline, store } });
		expect(container.querySelector('.dirty-dot')).toBeTruthy();
	});

	it('patches the goal when the number input changes', async () => {
		const { store, challenge, baseline } = setup({ statisticType: EStatisticType.EnemiesKilled });
		const { container } = render(GoalField, { props: { challenge, baseline, store } });
		await fireEvent.input(container.querySelector('input.num') as HTMLInputElement, { target: { value: '40' } });
		expect((store.items[0] as unknown as IChallenge).progressGoal).toBe(40);
	});
});
