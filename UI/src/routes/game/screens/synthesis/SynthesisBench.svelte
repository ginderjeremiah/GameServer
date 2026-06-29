<!-- The Synthesis bench (centre column): the selected recipe's inputs combining into its result, the
     proficiency conditions, and the state-driven call to action. The Synthesize button is enabled only
     for a `ready` recipe; `gated`/`hinted`/`done` render an explanatory disabled state instead. -->
<div class="bench">
	<div class="bench-label">Synthesis bench</div>

	{#if recipe}
		<div class="stage">
			<div class="inputs">
				{#each recipe.inputs as input, i (input.skillId)}
					{#if i > 0}<div class="plus" aria-hidden="true">+</div>{/if}
					<div class="input-card" class:owned={input.owned}>
						<SkillTile skill={input.skill} size={38} />
						<div class="input-meta">
							<div class="input-rarity">{input.skill ? rarityLabel(input.skill.rarityId) : 'Undiscovered'}</div>
							<div class="input-name">{input.skill?.name ?? '? ? ?'}</div>
						</div>
					</div>
				{/each}
			</div>

			<div class="arrow" aria-hidden="true">▶</div>

			<div class="result">
				{#if recipe.result}
					<SkillTile skill={recipe.result} size={50} />
					<div class="result-name">{recipe.result.name}</div>
					{#if recipe.result.word}
						<WordOfPower text={recipe.result.word} label={recipe.result.name} size={26} glow />
					{/if}
				{:else}
					<div class="sealed-glyph" aria-hidden="true">⟁</div>
					<div class="sealed-label">Undiscovered pairing</div>
				{/if}
			</div>
		</div>

		{#if recipe.result}
			<div class="conditions">
				<span class="cond-eyebrow">Conditions</span>
				<span class="chip ok">inputs {recipe.ownedInputCount} / {recipe.inputCount} ✓</span>
				{#each recipe.conditions as cond (cond.proficiencyId)}
					<span class="chip" class:ok={cond.met} class:bad={!cond.met}>
						{cond.name} Lv{cond.minLevel}
						{cond.met ? '✓' : `· now Lv${cond.currentLevel}`}
					</span>
				{/each}
			</div>
		{/if}

		<div class="cta">
			{#if recipe.state === 'ready'}
				<button
					type="button"
					class="synth"
					data-testid="synthesize-cta"
					disabled={synthesizing}
					onclick={() => onSynthesize(recipe)}
				>
					⟡ Synthesize
				</button>
				<div class="cta-note">one-time · <span class="kept">non-consumptive</span> · inputs are kept</div>
			{:else if recipe.state === 'gated'}
				<button type="button" class="locked" data-testid="cta-gated" disabled>▲ Gate locked</button>
				<div class="cta-note gate">{gateText}</div>
			{:else if recipe.state === 'hinted'}
				<button type="button" class="hinted" data-testid="cta-hinted" disabled>? Undiscovered</button>
				<div class="cta-note">{hintText}</div>
			{:else}
				<div class="done-row">
					<button type="button" class="view" onclick={onViewInSkills}>View in Skills</button>
				</div>
				<div class="cta-note synthd">✓ synthesized · now a normal skill</div>
			{/if}
		</div>
	{/if}
</div>

<script lang="ts">
import { rarityLabel } from '$lib/common';
import WordOfPower from '$components/WordOfPower.svelte';
import SkillTile from './SkillTile.svelte';
import type { RecipeView } from './synthesis';

type Props = {
	recipe: RecipeView | undefined;
	synthesizing: boolean;
	onSynthesize: (recipe: RecipeView) => void;
	onViewInSkills: () => void;
};

const { recipe, synthesizing, onSynthesize, onViewInSkills }: Props = $props();

/** The unmet proficiency conditions, worded as the gate. */
const gateText = $derived(
	(recipe?.conditions ?? [])
		.filter((c) => !c.met)
		.map((c) => `${c.name} Lv${c.currentLevel} → master to Lv${c.minLevel}`)
		.join(' · ')
);

/** The "you hold X, find the other" hint for a partially-owned recipe. */
const hintText = $derived.by(() => {
	const owned = (recipe?.inputs ?? []).filter((i) => i.skill).map((i) => i.skill?.name);
	return owned.length ? `you hold ${owned.join(', ')} — find the other reagent` : '';
});
</script>

<style lang="scss">
.bench {
	position: relative;
	display: flex;
	flex-direction: column;
	border: 1px solid var(--border-light);
	border-radius: 6px;
	background:
		radial-gradient(120% 60% at 50% -6%, color-mix(in srgb, var(--accent) 7%, transparent), transparent 62%),
		color-mix(in srgb, var(--white) 2%, transparent);
	padding: 16px 20px 20px;
	overflow: hidden;
	color: var(--text-primary);
	font-family: var(--sans);
}

.bench-label {
	flex-shrink: 0;
	text-align: center;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.stage {
	flex: 1;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 18px;
	min-height: 0;
}

.inputs {
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 9px;
}

.plus {
	font-family: var(--mono);
	font-size: 18px;
	color: var(--accent);
	line-height: 0.4;
}

.input-card {
	display: flex;
	align-items: center;
	gap: 10px;
	width: 190px;
	padding: 8px 12px 8px 8px;
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 2%, transparent);

	&.owned {
		border-color: var(--border-light);
	}
}

.input-meta {
	min-width: 0;
}

.input-rarity {
	font-family: var(--mono);
	font-size: 7.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.input-name {
	font-size: 14px;
	font-weight: 500;
	line-height: 1.1;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.arrow {
	font-size: 28px;
	color: var(--text-tertiary);
	line-height: 1;
}

.result {
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 10px;
	min-width: 170px;
	text-align: center;
	color: var(--accent);
}

.result-name {
	font-size: 26px;
	font-weight: 700;
	line-height: 0.95;
	color: var(--text-primary);
}

.sealed-glyph {
	font-size: 46px;
	line-height: 1;
	color: var(--accent);
}

.sealed-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.conditions {
	flex-shrink: 0;
	display: flex;
	align-items: center;
	gap: 9px;
	justify-content: center;
	flex-wrap: wrap;
	margin-bottom: 14px;
}

.cond-eyebrow {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.chip {
	font-family: var(--mono);
	font-size: 9px;
	padding: 4px 11px;
	border-radius: 20px;
	border: 1px solid var(--border-light);
	color: var(--text-secondary);

	&.ok {
		border-color: color-mix(in srgb, var(--success) 50%, transparent);
		color: var(--success);
	}

	&.bad {
		border-color: color-mix(in srgb, var(--gold) 50%, transparent);
		color: var(--gold);
	}
}

.cta {
	flex-shrink: 0;
	text-align: center;
}

button {
	font-family: var(--sans);
	cursor: pointer;
	border-radius: 4px;
}

.synth {
	font-size: 16px;
	font-weight: 600;
	padding: 11px 34px;
	background: var(--accent);
	color: var(--text-on-accent);
	border: 1px solid var(--accent);
	box-shadow: 0 0 18px color-mix(in srgb, var(--accent) 35%, transparent);

	&:disabled {
		opacity: 0.6;
		cursor: progress;
	}
}

.locked,
.hinted {
	font-size: 15px;
	font-weight: 500;
	padding: 11px 28px;
	background: transparent;
	cursor: not-allowed;
}

.locked {
	color: var(--gold);
	border: 1px solid color-mix(in srgb, var(--gold) 50%, transparent);
}

.hinted {
	color: var(--text-tertiary);
	border: 1px dashed var(--border-light);
}

.view {
	font-size: 14px;
	padding: 9px 18px;
	background: transparent;
	color: var(--text-primary);
	border: 1px solid var(--border-light);
}

.cta-note {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.3px;
	color: var(--text-muted);
	margin-top: 8px;

	&.gate {
		color: var(--gold);
	}

	&.synthd {
		color: var(--success);
	}

	.kept {
		color: var(--success);
	}
}
</style>
