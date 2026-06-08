import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EEntityType } from '$lib/api';

// WorkbenchReference reads its catalogues from the in-memory staticData store, so
// it is mocked here (mirrors statistics-view.test.ts).
const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ staticData }));

import { reference } from '$routes/admin/workbench/reference.svelte';

beforeEach(() => {
	staticData.enemies = [
		{ id: 0, name: 'Cave Bat', isBoss: false },
		{ id: 1, name: 'Catacomb Lich', isBoss: true },
		{ id: 2, name: 'Goblin', isBoss: false },
		{ id: 3, name: 'Ancient Wyrm', isBoss: true }
	];
	staticData.zones = [{ id: 0, name: 'Verdant Hollow' }];
	staticData.skills = [{ id: 0, name: 'Cleave' }];
});

describe('entityOptions / entityCatalog target-entity picker', () => {
	it('lists every enemy when the statistic is not boss-only', () => {
		expect(reference.entityOptions(EEntityType.Enemy).map((o) => o.value)).toEqual([0, 1, 2, 3]);
	});

	it('restricts the enemy picker to bosses for a boss-only statistic', () => {
		const opts = reference.entityOptions(EEntityType.Enemy, true);
		expect(opts.map((o) => o.text)).toEqual(['Catacomb Lich', 'Ancient Wyrm']);
	});

	it('ignores the boss-only flag for non-enemy dimensions', () => {
		expect(reference.entityOptions(EEntityType.Zone, true).map((o) => o.value)).toEqual([0]);
		expect(reference.entityOptions(EEntityType.Skill, true).map((o) => o.value)).toEqual([0]);
	});

	it('resolves a target name across all enemies regardless of the boss filter', () => {
		// entityName reads the unfiltered catalogue so an already-saved (or non-boss) target
		// still renders in the objective sentence.
		expect(reference.entityName(EEntityType.Enemy, 0)).toBe('Cave Bat');
		expect(reference.entityName(EEntityType.Enemy, 1)).toBe('Catacomb Lich');
	});
});
