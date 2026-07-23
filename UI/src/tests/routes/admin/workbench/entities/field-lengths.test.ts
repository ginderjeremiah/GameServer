import { describe, it, expect, vi } from 'vitest';
import {
	CHALLENGE_DESCRIPTION_MAX_LENGTH,
	CHALLENGE_DESIGNER_NOTES_MAX_LENGTH,
	CHALLENGE_NAME_MAX_LENGTH,
	CLASS_DESCRIPTION_MAX_LENGTH,
	CLASS_DESIGNER_NOTES_MAX_LENGTH,
	CLASS_NAME_MAX_LENGTH,
	CLASS_WORD_MAX_LENGTH,
	ENEMY_DESIGNER_NOTES_MAX_LENGTH,
	ENEMY_NAME_MAX_LENGTH,
	ITEM_DESCRIPTION_MAX_LENGTH,
	ITEM_DESIGNER_NOTES_MAX_LENGTH,
	ITEM_ICON_PATH_MAX_LENGTH,
	ITEM_MOD_DESCRIPTION_MAX_LENGTH,
	ITEM_MOD_DESIGNER_NOTES_MAX_LENGTH,
	ITEM_MOD_NAME_MAX_LENGTH,
	ITEM_NAME_MAX_LENGTH,
	LESSON_DESIGNER_NOTES_MAX_LENGTH,
	LESSON_KEY_MAX_LENGTH,
	LESSON_NAME_MAX_LENGTH,
	LESSON_SCREEN_KEY_MAX_LENGTH,
	LESSON_STEP_ANCHOR_KEY_MAX_LENGTH,
	LESSON_STEP_TEXT_MAX_LENGTH,
	SKILL_DESCRIPTION_MAX_LENGTH,
	SKILL_DESIGNER_NOTES_MAX_LENGTH,
	SKILL_ICON_PATH_MAX_LENGTH,
	SKILL_NAME_MAX_LENGTH,
	SKILL_PRONUNCIATION_MAX_LENGTH,
	SKILL_RECIPE_DESIGNER_NOTES_MAX_LENGTH,
	SKILL_TRANSLATION_MAX_LENGTH,
	SKILL_WORD_MAX_LENGTH,
	TAG_NAME_MAX_LENGTH,
	ZONE_DESCRIPTION_MAX_LENGTH,
	ZONE_DESIGNER_NOTES_MAX_LENGTH,
	ZONE_NAME_MAX_LENGTH
} from '$lib/api/types/game-constants';

/* Guards the Workbench maxLength convention (#2257) from drifting off the EF `HasMaxLength` bound it
   exists to mirror — the gap that quietly reopened three times (#2262/#2263, #2277, and the review of
   #2315). EXPECTED/EXPECTED_TABLE_COLUMNS are keyed against the generated, EF-sourced constants
   (`ContentFieldLengths` -> game-constants.ts) rather than a second hand-copied literal, so the two
   sides can no longer independently go stale. A text/textarea field with no entry here fails loudly
   instead of silently passing uncovered — the exact gap that let the value drift unnoticed before. */

const { staticData } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));
vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static post = vi.fn();
		static get = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: vi.fn(async () => []) };
});

import { workbenchEntities } from '$routes/admin/workbench/entities';

/** Expected `FieldConfig.maxLength` per entity key -> field key, sourced from `game-constants.ts`. */
const EXPECTED: Record<string, Record<string, number>> = {
	challenges: {
		name: CHALLENGE_NAME_MAX_LENGTH,
		description: CHALLENGE_DESCRIPTION_MAX_LENGTH,
		designerNotes: CHALLENGE_DESIGNER_NOTES_MAX_LENGTH
	},
	classes: {
		name: CLASS_NAME_MAX_LENGTH,
		word: CLASS_WORD_MAX_LENGTH,
		description: CLASS_DESCRIPTION_MAX_LENGTH,
		designerNotes: CLASS_DESIGNER_NOTES_MAX_LENGTH
	},
	enemies: {
		name: ENEMY_NAME_MAX_LENGTH,
		designerNotes: ENEMY_DESIGNER_NOTES_MAX_LENGTH
	},
	items: {
		name: ITEM_NAME_MAX_LENGTH,
		iconPath: ITEM_ICON_PATH_MAX_LENGTH,
		description: ITEM_DESCRIPTION_MAX_LENGTH,
		designerNotes: ITEM_DESIGNER_NOTES_MAX_LENGTH
	},
	itemMods: {
		name: ITEM_MOD_NAME_MAX_LENGTH,
		description: ITEM_MOD_DESCRIPTION_MAX_LENGTH,
		designerNotes: ITEM_MOD_DESIGNER_NOTES_MAX_LENGTH
	},
	skills: {
		name: SKILL_NAME_MAX_LENGTH,
		iconPath: SKILL_ICON_PATH_MAX_LENGTH,
		word: SKILL_WORD_MAX_LENGTH,
		pronunciation: SKILL_PRONUNCIATION_MAX_LENGTH,
		translation: SKILL_TRANSLATION_MAX_LENGTH,
		description: SKILL_DESCRIPTION_MAX_LENGTH,
		designerNotes: SKILL_DESIGNER_NOTES_MAX_LENGTH
	},
	skillRecipes: {
		designerNotes: SKILL_RECIPE_DESIGNER_NOTES_MAX_LENGTH
	},
	lessons: {
		key: LESSON_KEY_MAX_LENGTH,
		name: LESSON_NAME_MAX_LENGTH,
		screenKey: LESSON_SCREEN_KEY_MAX_LENGTH,
		designerNotes: LESSON_DESIGNER_NOTES_MAX_LENGTH
	},
	tags: {
		name: TAG_NAME_MAX_LENGTH
	},
	zones: {
		name: ZONE_NAME_MAX_LENGTH,
		description: ZONE_DESCRIPTION_MAX_LENGTH,
		designerNotes: ZONE_DESIGNER_NOTES_MAX_LENGTH
	}
};

/** Same shape as {@link EXPECTED}, but for a `table` section's text columns (keyed by entity -> section -> column). */
const EXPECTED_TABLE_COLUMNS: Record<string, Record<string, Record<string, number>>> = {
	lessons: {
		steps: {
			text: LESSON_STEP_TEXT_MAX_LENGTH,
			anchorKey: LESSON_STEP_ANCHOR_KEY_MAX_LENGTH
		}
	}
};

describe('Workbench text field/column maxLength matches its EF HasMaxLength bound', () => {
	for (const entity of workbenchEntities) {
		for (const section of entity.sections) {
			if (section.kind === 'fields') {
				for (const field of section.fields) {
					if (field.type !== 'text' && field.type !== 'textarea') {
						continue;
					}
					it(`${entity.key}.${field.key}`, () => {
						const expected = EXPECTED[entity.key]?.[field.key];
						expect(
							expected,
							`No expected maxLength registered for ${entity.key}.${field.key} — add the EF-mirrored ` +
								`constant to Game.Core/ContentFieldLengths.cs and this test's EXPECTED map.`
						).toBeDefined();
						expect(field.maxLength).toBe(expected);
					});
				}
			}

			if (section.kind === 'table') {
				for (const column of section.columns) {
					if (column.type !== 'text') {
						continue;
					}
					it(`${entity.key}.${section.key}.${column.key}`, () => {
						const expected = EXPECTED_TABLE_COLUMNS[entity.key]?.[section.key]?.[column.key];
						expect(
							expected,
							`No expected maxLength registered for ${entity.key}.${section.key}.${column.key} — add ` +
								`the EF-mirrored constant to Game.Core/ContentFieldLengths.cs and this test's ` +
								`EXPECTED_TABLE_COLUMNS map.`
						).toBeDefined();
						expect(column.maxLength).toBe(expected);
					});
				}
			}
		}
	}
});
