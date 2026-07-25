import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup } from '@testing-library/svelte';
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

/* The progression editor's half of the maxLength guard (#2377). `field-lengths.test.ts` under
   `entities/` walks `workbenchEntities`, so it never saw the bespoke Progression surface — its path and
   proficiency bounds were hand-copied literals mirroring equally hand-copied EF literals, the exact
   silent-drift class #2325 closed for the generic Workbench.

   Both sides now read `ContentFieldLengths` (via the generated `game-constants.ts`), so a *drifted* value
   is structurally impossible and `ProgInput.maxLength` is required so an *unbounded* input fails
   svelte-check. What neither catches is a field wired to the wrong constant, so this asserts the whole
   rendered field set of each surface by exact equality: a mis-wired bound, a new input registered against
   no constant, and a removed one all surface as a diff rather than passing silently. */

const { staticData, dangerModal } = vi.hoisted(() => ({
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
	},
	dangerModal: vi.fn()
}));
vi.mock('$stores', () => ({ staticData, dangerModal }));

import ConlangIdentity from '$routes/admin/workbench/progression/ConlangIdentity.svelte';
import PathDetail from '$routes/admin/workbench/progression/PathDetail.svelte';
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
	id: 0,
	name: 'Blades',
	description: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId: 5,
	pathOrdinal: 0,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1.4,
	designerNotes: '',
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: []
};

// Exposes only what the two identity surfaces read — the fake-store pattern PathDetail/TierDetail's
// own tests use, rather than driving the real ProgressionStore through a socket-backed load().
const store = {
	selectedPath: path,
	profs: [tier],
	paths: [path],
	currentTiers: [tier],
	pathTab: 'identity',
	saving: false,
	pathStatus: vi.fn(() => 'clean'),
	profStatus: vi.fn(() => 'clean'),
	isRetired: vi.fn(() => false),
	setPathTab: vi.fn(),
	resetPath: vi.fn(),
	retirePath: vi.fn(),
	removePath: vi.fn(),
	pathBaseline: vi.fn(() => path),
	patchPath: vi.fn(),
	patchProf: vi.fn()
} as unknown as ProgressionStore;

/** Every rendered text input/textarea keyed by its accessible label -> its `maxlength` (null when unbounded). */
const boundsOf = (container: HTMLElement): Record<string, number | null> =>
	Object.fromEntries(
		[...container.querySelectorAll('input, textarea')].map((element) => {
			const bound = element.getAttribute('maxlength');
			return [element.getAttribute('aria-label') ?? '(unlabelled)', bound === null ? null : Number(bound)];
		})
	);

afterEach(cleanup);

describe('Progression editor text field maxLength matches its EF HasMaxLength bound', () => {
	it('bounds every path identity field', () => {
		const { container } = render(PathDetail, { props: { store } });

		expect(boundsOf(container)).toEqual({
			'Path name': PATH_NAME_MAX_LENGTH,
			Description: PATH_DESCRIPTION_MAX_LENGTH,
			'Designer notes': PATH_DESIGNER_NOTES_MAX_LENGTH
		});
	});

	it('bounds every tier identity field', () => {
		const { container } = render(ConlangIdentity, { props: { store, tier } });

		expect(boundsOf(container)).toEqual({
			Name: PROFICIENCY_NAME_MAX_LENGTH,
			'Icon path': PROFICIENCY_ICON_PATH_MAX_LENGTH,
			Description: PROFICIENCY_DESCRIPTION_MAX_LENGTH,
			'Romanized word': PROFICIENCY_WORD_MAX_LENGTH,
			Pronunciation: PROFICIENCY_PRONUNCIATION_MAX_LENGTH,
			Translation: PROFICIENCY_TRANSLATION_MAX_LENGTH,
			'Designer notes': PROFICIENCY_DESIGNER_NOTES_MAX_LENGTH
		});
	});
});
