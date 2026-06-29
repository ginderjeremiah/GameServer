<!-- The Synthesis result dossier (right column): once the player owns every input the recipe's result
     is fully revealed — its word of power, rarity, damage/cooldown, scaling attributes and effects (the
     spike's "fully shown once you own all inputs"). A hinted recipe instead shows a sealed teaser, since
     its result identity stays masked until the inputs are gathered. -->
<div class="dossier sx-scroll">
	<div class="eyebrow">Result</div>

	{#if recipe?.result}
		{@const result = recipe.result}
		<div class="head">
			<SkillTile skill={result} size={44} />
			<div class="title-col">
				<div class="name">{result.name}</div>
				{#if result.word}
					<div class="word-line">
						<WordOfPower text={result.word} label={result.name} size={20} glow />
						<span class="word-meaning">{wordMeaning(result)}</span>
					</div>
				{/if}
			</div>
		</div>

		<div class="rarity-row">
			<span class="rarity-pill" style:--rarity={rarityColor(result.rarityId)}>{rarityLabel(result.rarityId)}</span>
		</div>

		<div class="stats">
			<div class="stat">
				<div class="v">{formatNum(result.baseDamage)}</div>
				<div class="k">Damage</div>
			</div>
			<div class="stat">
				<div class="v">{formatNum(result.cooldownMs / 1000)}s</div>
				<div class="k">Cooldown</div>
			</div>
			{#if scaling.length}
				<div class="stat">
					<div class="v scaling">
						{#each scaling as s, i (s.id)}<span style:color={s.color}>{s.code}{i < scaling.length - 1 ? '·' : ''}</span
							>{/each}
					</div>
					<div class="k">Scales</div>
				</div>
			{/if}
		</div>

		{#if result.description}
			<p class="description">{result.description}</p>
		{/if}

		{#if effects.length}
			<div class="effects">
				<div class="eyebrow">On hit</div>
				{#each effects as fx (fx.id)}
					<div class="effect" style:color={fx.color}>
						<span class="fx-mag">{fx.magnitude} {fx.attributeName}</span>
						<span class="fx-meta">{fx.targetLabel} · {fx.duration}</span>
					</div>
				{/each}
			</div>
		{/if}
	{:else if recipe}
		<div class="sealed">
			<div class="sealed-label">⟁ Sealed</div>
			<p class="sealed-copy">
				You hold one of its inputs. Find the other to reveal the formula — then synthesize it to learn its name and
				effect.
			</p>
		</div>
	{/if}
</div>

<script lang="ts">
import type { ISkill } from '$lib/api';
import {
	attributeCode,
	attributeColor,
	attributeIsHarmful,
	attributeName,
	describeEffect,
	effectDirectionColor,
	formatNum,
	rarityColor,
	rarityLabel
} from '$lib/common';
import { staticData } from '$stores';
import WordOfPower from '$components/WordOfPower.svelte';
import SkillTile from './SkillTile.svelte';
import type { RecipeView } from './synthesis';

type Props = {
	recipe: RecipeView | undefined;
};

const { recipe }: Props = $props();

/** The readable word line (pronunciation · translation), falling back to whichever is authored. */
const wordMeaning = (skill: ISkill): string => [skill.pronunciation, skill.translation].filter(Boolean).join(' · ');

/** The result's scaling attributes (code + themeable hue) for the "Scales" stat. */
const scaling = $derived(
	(recipe?.result?.damageMultipliers ?? []).map((m) => ({
		id: m.attributeId,
		code: attributeCode(m.attributeId, staticData.attributes),
		color: attributeColor(m.attributeId)
	}))
);

/** The result's authored effects, worded through the shared display helper so the buff/debuff signal
 *  matches every other effect surface. */
const effects = $derived(
	(recipe?.result?.effects ?? []).map((effect) => {
		const desc = describeEffect(
			effect,
			attributeName(effect.attributeId, staticData.attributes),
			attributeIsHarmful(effect.attributeId, staticData.attributes)
		);
		return {
			id: effect.id,
			magnitude: desc.magnitude,
			attributeName: desc.attributeName,
			targetLabel: desc.targetLabel,
			duration: desc.duration,
			color: effectDirectionColor(desc.direction)
		};
	})
);
</script>

<style lang="scss">
.dossier {
	overflow-y: auto;
	background: color-mix(in srgb, var(--white) 2%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 15px 16px 18px;
	color: var(--text-primary);
	font-family: var(--sans);
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.head {
	display: flex;
	align-items: center;
	gap: 12px;
	margin-top: 11px;
}

.title-col {
	min-width: 0;
}

.name {
	font-size: 22px;
	font-weight: 700;
	line-height: 1.05;
}

.word-line {
	display: flex;
	align-items: center;
	gap: 9px;
	margin-top: 5px;
	color: var(--accent);
}

.word-meaning {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.5px;
	color: var(--text-tertiary);
}

.rarity-row {
	margin-top: 12px;
}

.rarity-pill {
	font-family: var(--mono);
	font-size: 8.5px;
	font-weight: 600;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	padding: 2px 9px;
	border-radius: 12px;
	color: var(--rarity);
	border: 1px solid color-mix(in srgb, var(--rarity) 50%, transparent);
	background: color-mix(in srgb, var(--rarity) 12%, transparent);
}

.stats {
	display: flex;
	gap: 22px;
	margin-top: 16px;
	padding-top: 14px;
	border-top: 1px solid var(--border-subtle);

	.v {
		font-size: 21px;
		font-weight: 500;
		line-height: 1;

		&.scaling {
			display: flex;
			gap: 2px;
			font-size: 18px;
		}
	}

	.k {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 1.2px;
		text-transform: uppercase;
		color: var(--text-muted);
		margin-top: 4px;
	}
}

.description {
	margin: 13px 0 0;
	font-size: 12.5px;
	line-height: 1.5;
	color: var(--text-secondary);
}

.effects {
	margin-top: 16px;
	padding-top: 14px;
	border-top: 1px solid var(--border-subtle);
	display: flex;
	flex-direction: column;
	gap: 7px;

	.eyebrow {
		margin-bottom: 2px;
	}
}

.effect {
	display: flex;
	flex-direction: column;
	gap: 1px;
}

.fx-mag {
	font-size: 12.5px;
	font-weight: 500;
}

.fx-meta {
	font-family: var(--mono);
	font-size: 9px;
	color: var(--text-muted);
}

.sealed {
	margin-top: 16px;
	padding: 13px 14px;
	border: 1px dashed var(--border-light);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 2%, transparent);
}

.sealed-label {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--accent);
	margin-bottom: 6px;
}

.sealed-copy {
	margin: 0;
	font-size: 12.5px;
	line-height: 1.45;
	color: var(--text-secondary);
}
</style>
