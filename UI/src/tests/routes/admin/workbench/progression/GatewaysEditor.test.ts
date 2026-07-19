import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';

import GatewaysEditor from '$routes/admin/workbench/progression/GatewaysEditor.svelte';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchPath, WorkbenchProficiency } from '$routes/admin/workbench/progression/types';
import { EActivityKey } from '$lib/api';

afterEach(cleanup);

const tier = (over: Partial<WorkbenchProficiency> = {}): WorkbenchProficiency => ({
	id: 0,
	name: 'Blades',
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId: 0,
	pathOrdinal: 0,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	designerNotes: '',
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...over
});

const path = (over: Partial<WorkbenchPath> = {}): WorkbenchPath => ({
	id: 0,
	name: 'Fire',
	description: '',
	activityKey: EActivityKey.Fire,
	designerNotes: '',
	...over
});

// A fake store exposing exactly what GatewaysEditor reads/calls — mirrors TierDetail.test.ts's
// fake-store pattern rather than driving the real ProgressionStore through a socket-backed load().
const makeStore = (profs: WorkbenchProficiency[], paths: WorkbenchPath[] = []) =>
	({
		profs,
		paths,
		addPrerequisite: vi.fn(),
		removePrerequisite: vi.fn()
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
	}) as any as ProgressionStore;

describe('GatewaysEditor — cross-path-only prerequisite picker (#2128)', () => {
	it('excludes tiers of the gated tier\'s own path from the "Add prerequisite" options', async () => {
		const gated = tier({ id: 0, pathId: 0, name: 'Fire I' });
		const sibling = tier({ id: 1, pathId: 0, pathOrdinal: 1, name: 'Fire II' });
		const crossPathTier = tier({ id: 2, pathId: 1, name: 'Frost I' });
		const store = makeStore([gated, sibling, crossPathTier]);

		render(GatewaysEditor, { store, tier: gated });

		const select = screen.getByRole('combobox', { name: 'Add prerequisite' }) as HTMLSelectElement;
		const optionTexts = Array.from(select.options).map((o) => o.text);

		expect(optionTexts).toContain('Frost I');
		expect(optionTexts).not.toContain('Fire II');
	});

	it('still excludes an already-added cross-path prerequisite and a retired cross-path tier', async () => {
		const gated = tier({ id: 0, pathId: 0, prerequisiteIds: [2] });
		const alreadyAdded = tier({ id: 2, pathId: 1, name: 'Frost I' });
		const retiredCrossPath = tier({ id: 3, pathId: 1, name: 'Frost II', retiredAt: '2026-01-01T00:00:00Z' });
		const openCrossPath = tier({ id: 4, pathId: 2, name: 'Wind I' });
		const store = makeStore([gated, alreadyAdded, retiredCrossPath, openCrossPath]);

		render(GatewaysEditor, { store, tier: gated });

		const select = screen.getByRole('combobox', { name: 'Add prerequisite' }) as HTMLSelectElement;
		const optionTexts = Array.from(select.options).map((o) => o.text);

		expect(optionTexts).toContain('Wind I');
		expect(optionTexts).not.toContain('Frost I');
		expect(optionTexts).not.toContain('Frost II');
	});
});

describe('GatewaysEditor — excludes tiers of a retired path (#2176)', () => {
	it('omits a tier whose own record is live but whose owning path is retired', async () => {
		const gated = tier({ id: 0, pathId: 0, name: 'Fire I' });
		const frozenTier = tier({ id: 1, pathId: 1, name: 'Frost I' });
		const openTier = tier({ id: 2, pathId: 2, name: 'Wind I' });
		const paths = [
			path({ id: 0, name: 'Fire' }),
			path({ id: 1, name: 'Frost', retiredAt: '2026-01-01T00:00:00Z' }),
			path({ id: 2, name: 'Wind' })
		];
		const store = makeStore([gated, frozenTier, openTier], paths);

		render(GatewaysEditor, { store, tier: gated });

		const select = screen.getByRole('combobox', { name: 'Add prerequisite' }) as HTMLSelectElement;
		const optionTexts = Array.from(select.options).map((o) => o.text);

		expect(optionTexts).toContain('Wind I');
		expect(optionTexts).not.toContain('Frost I');
	});
});
