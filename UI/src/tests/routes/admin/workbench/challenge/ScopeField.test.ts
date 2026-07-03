import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EChallengeType, EEntityType, type IChallenge } from '$lib/api';

const { mockReference } = vi.hoisted(() => ({
	mockReference: {
		challengeTypes: [] as { id: number; statisticType?: { bossOnly?: boolean } }[],
		// eslint-disable-next-line @typescript-eslint/no-unused-vars
		entityOptions: vi.fn((entityType: number, bossOnly = false) => [
			{ value: 0, text: 'Cave Bat' },
			{ value: 1, text: 'Catacomb Lich' }
		])
	}
}));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: mockReference }));
vi.mock('$stores', () => ({ toastError: vi.fn() }));

import ScopeField from '$routes/admin/workbench/components/challenge/ScopeField.svelte';
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
		entityType: EEntityType.Enemy,
		progressGoal: 10,
		...over
	} as IChallenge;
	const store = new EntityStore(config(), [challenge as unknown as Identified]);
	const record = store.items[0] as unknown as IChallenge;
	return { store, challenge: record, baseline: store.baselineOf(1) as unknown as IChallenge };
};

beforeEach(() => {
	mockReference.challengeTypes = [];
	mockReference.entityOptions.mockClear();
});

afterEach(cleanup);

describe('ScopeField — global / stat-less statistic', () => {
	it('collapses to a "tracked globally" read-out when the type has no entity dimension', () => {
		const { store, challenge, baseline } = setup({ entityType: EEntityType.None });
		render(ScopeField, { props: { challenge, baseline, store } });
		expect(screen.getByText(/Tracked globally/)).toBeTruthy();
	});

	it('reads "tracked from player level" for Level Reached', () => {
		const { store, challenge, baseline } = setup({
			entityType: EEntityType.None,
			challengeTypeId: EChallengeType.LevelReached
		});
		render(ScopeField, { props: { challenge, baseline, store } });
		expect(screen.getByText(/Tracked from player level/)).toBeTruthy();
	});
});

describe('ScopeField — entity-scoped statistic', () => {
	it('defaults to Global with no target select', () => {
		const { store, challenge, baseline } = setup({ entityType: EEntityType.Enemy, targetEntityId: undefined });
		const { container } = render(ScopeField, { props: { challenge, baseline, store } });
		const [global, specific] = Array.from(container.querySelectorAll('.eswitch button'));
		expect(global.classList.contains('active')).toBe(true);
		expect(specific.classList.contains('active')).toBe(false);
		expect(container.querySelector('.scope-select')).toBeNull();
	});

	it('shows the target select when a specific entity is chosen', () => {
		const { store, challenge, baseline } = setup({ entityType: EEntityType.Enemy, targetEntityId: 1 });
		const { container } = render(ScopeField, { props: { challenge, baseline, store } });
		expect(screen.getByText('Scope · Enemy')).toBeTruthy();
		const select = container.querySelector('.scope-select select') as HTMLSelectElement;
		expect(select.value).toBe('1');
	});

	it('switching to Specific sets the target to the first option', async () => {
		const { store, challenge, baseline } = setup({ entityType: EEntityType.Enemy, targetEntityId: undefined });
		render(ScopeField, { props: { challenge, baseline, store } });
		await fireEvent.click(screen.getByText('Specific'));
		expect((store.items[0] as unknown as IChallenge).targetEntityId).toBe(0);
	});

	it('switching to Global clears the target', async () => {
		const { store, challenge, baseline } = setup({ entityType: EEntityType.Enemy, targetEntityId: 1 });
		render(ScopeField, { props: { challenge, baseline, store } });
		await fireEvent.click(screen.getByText('Global'));
		expect((store.items[0] as unknown as IChallenge).targetEntityId).toBeUndefined();
	});

	it('changing the select patches the target id', async () => {
		const { store, challenge, baseline } = setup({ entityType: EEntityType.Enemy, targetEntityId: 0 });
		const { container } = render(ScopeField, { props: { challenge, baseline, store } });
		await fireEvent.change(container.querySelector('.scope-select select') as HTMLSelectElement, {
			target: { value: '1' }
		});
		expect((store.items[0] as unknown as IChallenge).targetEntityId).toBe(1);
	});
});

describe('ScopeField — dirty state', () => {
	it('is not dirty when a local clear (undefined) matches a server baseline stored as null', () => {
		// The server baseline serializes an unset optional as null, while `setGlobal` (and a fresh
		// clone) leaves the field simply absent — the two must read as equal, not phantom-dirty.
		const { store, baseline } = setup({
			entityType: EEntityType.Enemy,
			targetEntityId: null as unknown as number
		});
		store.patch(1, (d) => ((d as unknown as IChallenge).targetEntityId = undefined));
		const challenge = store.items[0] as unknown as IChallenge;

		const { container } = render(ScopeField, { props: { challenge, baseline, store } });
		expect(container.querySelector('.dirty-dot')).toBeNull();
	});

	it('is dirty when the target actually differs from baseline', () => {
		const { store, baseline } = setup({ entityType: EEntityType.Enemy, targetEntityId: 0 });
		store.patch(1, (d) => ((d as unknown as IChallenge).targetEntityId = 1));
		const challenge = store.items[0] as unknown as IChallenge;

		const { container } = render(ScopeField, { props: { challenge, baseline, store } });
		expect(container.querySelector('.dirty-dot')).toBeTruthy();
	});
});
