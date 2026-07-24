import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import { EActivityKey } from '$lib/api';
import {
	PATH_DESCRIPTION_MAX_LENGTH,
	PATH_DESIGNER_NOTES_MAX_LENGTH,
	PATH_NAME_MAX_LENGTH,
	PROFICIENCY_DESCRIPTION_MAX_LENGTH,
	PROFICIENCY_DESIGNER_NOTES_MAX_LENGTH,
	PROFICIENCY_ICON_PATH_MAX_LENGTH,
	PROFICIENCY_NAME_MAX_LENGTH,
	PROFICIENCY_PRONUNCIATION_MAX_LENGTH,
	PROFICIENCY_TRANSLATION_MAX_LENGTH,
	PROFICIENCY_WORD_MAX_LENGTH
} from '$lib/api/types/game-constants';

/* Guards the progression editor's (Paths/Proficiencies) hand-copied ProgInput maxLength props from
   drifting off their EF HasMaxLength bound (Game.Core/ContentFieldLengths.cs), mirroring the same
   convention field-lengths.test.ts enforces for the generic Workbench (#2257). The progression editor
   is a bespoke surface, not a FieldConfig-driven EntityConfig (docs/frontend-admin.md), so it isn't
   walked by that test's workbenchEntities loop — this fills that gap (#2377). */

const { staticData } = vi.hoisted(() => ({
	staticData: {
		enemies: [] as unknown[],
		zones: [] as unknown[],
		challenges: [] as unknown[],
		items: [] as unknown[],
		classes: [] as unknown[],
		skillRecipes: [] as unknown[],
		proficiencies: [] as unknown[],
		skills: [] as unknown[],
		paths: [] as unknown[]
	}
}));
vi.mock('$stores', () => ({ staticData }));

import PathDetail from '$routes/admin/workbench/progression/PathDetail.svelte';
import TierDetail from '$routes/admin/workbench/progression/TierDetail.svelte';
import type { ProgressionStore } from '$routes/admin/workbench/progression/progression-store.svelte';
import type { WorkbenchPath, WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

const path: WorkbenchPath = {
	id: 5,
	name: 'Fire Path',
	description: '',
	designerNotes: '',
	activityKey: EActivityKey.Fire
};

const tier: WorkbenchProficiency = {
	id: 5,
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
	prerequisiteIds: []
};

const pathStore = () =>
	({
		selectedPath: path,
		profs: [],
		paths: [],
		currentTiers: [],
		pathTab: 'identity',
		saving: false,
		pathStatus: vi.fn(() => 'clean'),
		isRetired: vi.fn(() => false),
		pathBaseline: vi.fn(() => path),
		patchPath: vi.fn()
	}) as unknown as ProgressionStore;

const tierStore = () =>
	({
		drilledTier: tier,
		profs: [tier],
		paths: [],
		tierTab: 'identity',
		selectedPath: path,
		profStatus: vi.fn(() => 'clean'),
		isRetired: vi.fn(() => false),
		profBaseline: vi.fn(() => tier),
		patchProf: vi.fn()
	}) as unknown as ProgressionStore;

afterEach(cleanup);

describe('Progression editor text field maxLength matches its EF HasMaxLength bound (#2377)', () => {
	it('Path identity fields', () => {
		render(PathDetail, { props: { store: pathStore() } });

		expect((screen.getByLabelText('Path name') as HTMLInputElement).maxLength).toBe(PATH_NAME_MAX_LENGTH);
		expect((screen.getByLabelText('Description') as HTMLTextAreaElement).maxLength).toBe(PATH_DESCRIPTION_MAX_LENGTH);
		expect((screen.getByLabelText('Designer notes') as HTMLTextAreaElement).maxLength).toBe(
			PATH_DESIGNER_NOTES_MAX_LENGTH
		);
	});

	it('Proficiency (tier) conlang identity fields', () => {
		render(TierDetail, { props: { store: tierStore() } });

		expect((screen.getByLabelText('Name') as HTMLInputElement).maxLength).toBe(PROFICIENCY_NAME_MAX_LENGTH);
		expect((screen.getByLabelText('Icon path') as HTMLInputElement).maxLength).toBe(PROFICIENCY_ICON_PATH_MAX_LENGTH);
		expect((screen.getByLabelText('Romanized word') as HTMLInputElement).maxLength).toBe(PROFICIENCY_WORD_MAX_LENGTH);
		expect((screen.getByLabelText('Pronunciation') as HTMLInputElement).maxLength).toBe(
			PROFICIENCY_PRONUNCIATION_MAX_LENGTH
		);
		expect((screen.getByLabelText('Translation') as HTMLInputElement).maxLength).toBe(
			PROFICIENCY_TRANSLATION_MAX_LENGTH
		);
		expect((screen.getByLabelText('Description') as HTMLTextAreaElement).maxLength).toBe(
			PROFICIENCY_DESCRIPTION_MAX_LENGTH
		);
		expect((screen.getByLabelText('Designer notes') as HTMLTextAreaElement).maxLength).toBe(
			PROFICIENCY_DESIGNER_NOTES_MAX_LENGTH
		);
	});
});
