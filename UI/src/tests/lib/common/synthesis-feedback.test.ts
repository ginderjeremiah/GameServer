import { describe, it, expect } from 'vitest';
import { creatableRecipeIds, isRecipeCreatable, recipeAvailableMessage } from '$lib/common';
import type { ISkillRecipe } from '$lib/api';

/* The derivation takes explicit args and needs no mocks. Recipes are zero-based-id reference data, so the
   fixtures keep ids contiguous with their array position (mirroring the reveal test). */

const recipe = (
	id: number,
	resultSkillId: number,
	inputSkillIds: number[],
	conditions: { proficiencyId: number; minLevel: number }[] = [],
	over: Partial<ISkillRecipe> = {}
): ISkillRecipe => ({ id, resultSkillId, designerNotes: '', inputSkillIds, conditions, ...over });

const owned = (...ids: number[]) => new Set(ids);
const levels = (entries: [number, number][] = []) => new Map(entries);

describe('isRecipeCreatable', () => {
	it('is creatable once every input is owned, the result is not, and there are no conditions', () => {
		expect(isRecipeCreatable(recipe(0, 4, [0, 1]), owned(0, 1), levels())).toBe(true);
	});

	it('is not creatable while an input is still missing', () => {
		expect(isRecipeCreatable(recipe(0, 4, [0, 1]), owned(0), levels())).toBe(false);
	});

	it('is not creatable once the result skill is already owned (synthesis is one-time)', () => {
		expect(isRecipeCreatable(recipe(0, 4, [0, 1]), owned(0, 1, 4), levels())).toBe(false);
	});

	it('requires every proficiency condition to be met (a missing proficiency counts as level 0)', () => {
		const gated = recipe(0, 5, [0, 2], [{ proficiencyId: 0, minLevel: 5 }]);
		expect(isRecipeCreatable(gated, owned(0, 2), levels([[0, 4]]))).toBe(false);
		expect(isRecipeCreatable(gated, owned(0, 2), levels())).toBe(false);
		expect(isRecipeCreatable(gated, owned(0, 2), levels([[0, 5]]))).toBe(true);
	});
});

describe('creatableRecipeIds', () => {
	it('collects the ids of every currently-creatable recipe', () => {
		const recipes = [
			recipe(0, 4, [0, 1]), // ready
			recipe(1, 5, [0, 2], [{ proficiencyId: 0, minLevel: 5 }]), // gated (level 3 < 5)
			recipe(2, 6, [3, 7]), // missing input 7
			recipe(3, 8, [0]) // ready
		];
		const ids = creatableRecipeIds(recipes, owned(0, 1, 2, 3), levels([[0, 3]]));
		expect([...ids].sort((a, b) => a - b)).toEqual([0, 3]);
	});

	it('excludes retired recipes — a retired recipe stops being offered', () => {
		const recipes = [recipe(0, 4, [0, 1], [], { retiredAt: '2026-01-01T00:00:00Z' })];
		expect(creatableRecipeIds(recipes, owned(0, 1), levels()).size).toBe(0);
	});

	it('is empty when nothing is creatable', () => {
		expect(creatableRecipeIds([recipe(0, 4, [0, 1])], owned(0), levels()).size).toBe(0);
	});
});

describe('recipeAvailableMessage', () => {
	it('names the result skill the player can now synthesize', () => {
		expect(recipeAvailableMessage('Frostfire')).toBe('New recipe available: Frostfire');
	});
});
