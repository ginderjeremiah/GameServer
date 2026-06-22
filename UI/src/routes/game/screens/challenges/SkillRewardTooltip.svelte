<!-- A lightweight, reference-data-only preview of a skill reward, composed over the shared tooltip
	chrome. Unlike the battle `SkillTooltip` it needs no `Battler`/owner context (there is no live
	fight when inspecting a challenge reward), so it reads the authored `ISkill` directly. The same
	component serves the sealed teaser (`masked`): redacted rows that keep their counts truthful. -->
<TooltipShell accent={rarityAccent} glow={!masked}>
	{#snippet header()}
		<TooltipTitle
			label="Skill"
			name={skill.name}
			diamondColor="var(--accent)"
			labelColor="var(--accent)"
			{masked}
			sealedAccent={rarityAccent}
		/>
	{/snippet}

	{#if masked}
		{#if skill.damageMultipliers.length}
			<TooltipSection label="Scaling">
				<TooltipStatsGrid
					masked
					maskedRows={skill.damageMultipliers.length}
					accent={rarityAccent}
					barWidths={SCALING_BAR_WIDTHS}
				/>
			</TooltipSection>
		{/if}

		{#if skill.effects.length}
			<TooltipSection label="On hit">
				<TooltipStatsGrid
					masked
					maskedRows={skill.effects.length}
					accent={rarityAccent}
					barWidths={EFFECT_BAR_WIDTHS}
				/>
			</TooltipSection>
		{/if}

		<TooltipSection label="Description" last>
			<TooltipDescription masked accent={rarityAccent} lineWidths={DESC_LINE_WIDTHS} />
		</TooltipSection>
	{:else}
		<TooltipSection label="Damage">
			<div class="srt-line">
				<span class="srt-key">Base damage</span>
				<span class="srt-val">{formatNum(skill.baseDamage)}</span>
			</div>
			{#each scaling as entry (entry.attributeId)}
				<div class="srt-line">
					<span class="srt-key" style:color={attributeColor(entry.attributeId)}>{entry.name}</span>
					<span class="srt-val">×{formatNum(entry.multiplier)}</span>
				</div>
			{/each}
		</TooltipSection>

		<TooltipSection label="Cooldown" last={effectLines.length === 0 && !skill.description}>
			<div class="srt-line">
				<span class="srt-key">Cooldown</span>
				<span class="srt-val">{cooldownSeconds}s</span>
			</div>
		</TooltipSection>

		{#if effectLines.length}
			<TooltipSection label="On hit" last={!skill.description}>
				{#each effectLines as effect (effect.id)}
					<div class="srt-effect">
						<span class="srt-mag" style:color={effectDirectionColor(effect.direction)}>{effect.magnitude}</span>
						<span class="srt-attr">{effect.attributeName}</span>
						<span class="srt-meta">{effect.targetLabel} · {effect.duration}</span>
					</div>
				{/each}
			</TooltipSection>
		{/if}

		{#if skill.description}
			<TooltipSection label="Description" last>
				<TooltipDescription text={skill.description} />
			</TooltipSection>
		{/if}
	{/if}
</TooltipShell>

<script lang="ts">
import type { ISkill } from '$lib/api';
import {
	attributeColor,
	attributeIsHarmful,
	attributeName,
	describeEffect,
	effectDirectionColor,
	formatNum,
	rarityColor
} from '$lib/common';
import { staticData } from '$stores';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import TooltipSection from '$components/tooltip/TooltipSection.svelte';
import TooltipStatsGrid from '$components/tooltip/TooltipStatsGrid.svelte';
import TooltipTitle from '$components/tooltip/TooltipTitle.svelte';
import TooltipDescription from '$components/tooltip/TooltipDescription.svelte';

interface Props {
	skill: ISkill;
	/** Render a sealed teaser (masked name, redacted scaling/effects/description) instead of the real skill. */
	masked?: boolean;
}

const { skill, masked = false }: Props = $props();

// The tooltip's main accent (left border) and sealed teaser reflect the skill's rarity tier, mirroring
// the item/mod reward tooltips; the diamond/label keep the neutral skill accent (skills have no
// secondary type classifier like an item category).
const rarityAccent = $derived(rarityColor(skill.rarityId));

const scaling = $derived(
	skill.damageMultipliers.map((m) => ({
		attributeId: m.attributeId,
		name: attributeName(m.attributeId, staticData.attributes),
		multiplier: m.multiplier
	}))
);
const cooldownSeconds = $derived(formatNum(skill.cooldownMs / 1000));
// One display line per authored effect, reusing the shared helper so wording/direction match the
// battle tooltip's "On hit" lines; `id` is kept for a stable each-key.
const effectLines = $derived(
	skill.effects.map((effect) => ({
		id: effect.id,
		...describeEffect(
			effect,
			attributeName(effect.attributeId, staticData.attributes),
			attributeIsHarmful(effect.attributeId, staticData.attributes)
		)
	}))
);

const SCALING_BAR_WIDTHS = [82, 64, 92];
const EFFECT_BAR_WIDTHS = [70, 88, 58];
const DESC_LINE_WIDTHS = [236, 180];
</script>

<style lang="scss">
.srt-line {
	display: flex;
	justify-content: space-between;
	align-items: baseline;
	font-size: 12px;

	& + & {
		margin-top: 4px;
	}
}

.srt-key {
	color: var(--text-secondary);
}

.srt-val {
	font-family: var(--mono);
	font-size: 11.5px;
	color: color-mix(in srgb, var(--text-primary) 70%, transparent);
}

.srt-effect {
	display: flex;
	align-items: baseline;
	gap: 10px;
	font-size: 12px;

	& + & {
		margin-top: 4px;
	}
}

.srt-mag {
	min-width: 42px;
	font-family: var(--mono);
	font-size: 11px;
	font-weight: 600;
	text-align: right;
}

.srt-attr {
	color: var(--text-secondary);
}

.srt-meta {
	margin-left: auto;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.4px;
	color: var(--text-muted);
	white-space: nowrap;
}
</style>
