import { ApiRequest, fetchSocketData, type ISkillRecipe } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, guardedSave, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import { chipsSection, type EntityConfig } from './types';

/**
 * The admin Workbench editor for skill-synthesis recipes (spike #1125, area A). A recipe is nameless
 * reference data — it is identified by its result skill — so the list/detail title is derived through the
 * {@link EntityConfig.title} hook rather than a stored name. The identity save carries only the result skill
 * (and retirement); the input skills and proficiency conditions are persisted through their own relationship
 * setters (the `persistEntity` child-saver pattern, mirroring how the class editor saves its child collections).
 *
 * The result/input pickers exclude retired skills (a hard block; an already-authored value stays visible), and
 * the result picker is further restricted to Synthesis-flagged skills. The remaining authoritative checks
 * (acyclicity/reachability, an input equal to the result, the proficiency-level range) live on the backend and
 * surface as a save-failure toast; the cheap, local rules are flagged as warnings here.
 */
// Written through to staticData.skillRecipes so retire-confirm's reference computation (recipe-result/
// recipe-input/recipe-condition groups) sees post-save edits, mirroring how entities/skill.ts writes through (#1633).
const refresh = async (): Promise<ISkillRecipe[]> => {
	const recipes = await fetchSocketData('GetSkillRecipes');
	staticData.skillRecipes = recipes;
	return recipes;
};

/** The result skill's name, the recipe's identity in the list/detail title and the referenced-by dialog. */
const resultName = (recipe: ISkillRecipe): string => reference.skillName(recipe.resultSkillId) ?? '';

export const skillRecipeEntity: EntityConfig<ISkillRecipe> = {
	key: 'skillRecipes',
	label: 'Recipes',
	singular: 'Recipe',
	glyph: 'bolt',
	blankName: 'New recipe',
	retireable: true,
	newItem: (id) => ({
		id,
		// Default to the first authorable Synthesis result; -1 (no valid option yet) surfaces as a warning.
		resultSkillId: reference.synthesisResultSkillOptions()[0]?.value ?? -1,
		designerNotes: '',
		inputSkillIds: [],
		conditions: []
	}),
	title: (r) => resultName(r),
	// The recipe formula reads as "input + input → result"; an input-less recipe shows the gap explicitly.
	headline: (r) => {
		const inputs = r.inputSkillIds.map((id) => reference.skillName(id) ?? `#${id}`);
		return `${inputs.length ? inputs.join(' + ') : '(no inputs)'} → ${resultName(r) || '(no result)'}`;
	},
	meta: (r) => [
		['inputs', r.inputSkillIds.length],
		['cond', r.conditions.length]
	],
	sections: [
		{
			key: 'result',
			label: 'Result',
			glyph: 'rune',
			desc: 'The skill this recipe produces',
			kind: 'fields',
			// `required` only guards an empty value; this section-level check also catches a result that
			// resolves to a non-Synthesis or retired skill (e.g. an authored result that later lost its
			// flag) — the same condition AdminSkillRecipes hard-rejects on save, so it blocks (#2217).
			warn: (r) =>
				reference.isSynthesisResult(r.resultSkillId)
					? null
					: { message: 'Result must be a live Synthesis-flagged skill', blocking: true },
			fields: [
				{
					key: 'resultSkillId',
					label: 'Result Skill',
					type: 'select',
					options: reference.synthesisResultSkillOptions,
					width: 260
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this recipe exists — authoring notes (never shown to players)…',
					grow: true
				}
			]
		},
		chipsSection<ISkillRecipe>()({
			key: 'inputs',
			label: 'Inputs',
			glyph: 'box',
			desc: 'Owned skills combined into the result',
			count: (r) => r.inputSkillIds.length,
			// Both hard-rejected by AdminSkillRecipes.SetInputs, so they block Save (#2217).
			warn: (r) =>
				r.inputSkillIds.length === 0
					? { message: 'No input skills', blocking: true }
					: r.inputSkillIds.includes(r.resultSkillId)
						? { message: 'An input cannot be the result skill', blocking: true }
						: null,
			kind: 'chips',
			itemsKey: 'inputSkillIds',
			// Any non-retired skill can be an input; a retired skill stays visible as a removable chip but
			// can't be newly added (the backend rejects a retired input too).
			catalogue: () => reference.skillCatalogue().map((s) => ({ ...s, addable: !s.retired })),
			labelOf: (s) => s.name,
			metaOf: (s) => `${s.baseDamage} dmg`,
			emptyIcon: 'box',
			emptyTitle: 'No input skills',
			emptySub: 'A recipe combines at least one owned skill.',
			addLabel: 'Add input skill…'
		}),
		{
			key: 'conditions',
			label: 'Conditions',
			glyph: 'gauge',
			desc: 'Proficiency-level gates that must be met',
			count: (r) => r.conditions.length,
			// Hard-rejected by AdminSkillRecipes.SetConditions, so it blocks Save (#2217).
			warn: (r) =>
				r.conditions.some((c) => c.minLevel < 1)
					? { message: 'Condition level must be at least 1', blocking: true }
					: null,
			kind: 'table',
			itemsKey: 'conditions',
			rowKey: 'proficiencyId',
			addLabel: 'Add condition',
			emptyIcon: 'gauge',
			emptyTitle: 'No conditions',
			emptySub: 'This recipe has no proficiency gates.',
			newRow: (r) => ({
				proficiencyId: firstFree(
					r.conditions.map((c) => c.proficiencyId),
					reference.proficiencyOptions()
				),
				minLevel: 1
			}),
			columns: [
				{
					key: 'proficiencyId',
					label: 'Proficiency',
					type: 'select',
					options: reference.proficiencyOptions,
					min: 220,
					unique: true
				},
				{ key: 'minLevel', label: 'Min Level', type: 'number', align: 'r', width: 120 }
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			// The identity DTO carries only the result skill (+ retirement); the child collections are saved
			// through their own setters, so they are emptied here. The C# contract requires both keys, so they
			// are sent (empty) rather than omitted.
			toPrimaryDto: (r) => ({
				id: r.id,
				resultSkillId: r.resultSkillId,
				designerNotes: r.designerNotes,
				retiredAt: r.retiredAt,
				inputSkillIds: [],
				conditions: []
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditSkillRecipes', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) =>
					guardedSave(childChanged(record.inputSkillIds, baseline?.inputSkillIds), () =>
						ApiRequest.post('AdminTools/SetSkillRecipeInputs', { id, skillIds: record.inputSkillIds })
					),
				async (id, record, baseline) =>
					guardedSave(childChanged(record.conditions, baseline?.conditions), () =>
						ApiRequest.post('AdminTools/SetSkillRecipeConditions', { id, conditions: record.conditions })
					)
			]
		})
};
