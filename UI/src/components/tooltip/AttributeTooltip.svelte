<!-- A reusable attribute tooltip on the shared tooltip chrome: the attribute's icon + name + type
     (Primary/Secondary/Status) + description, read from the `Attributes` reference data. When opened
     from a combat effect chip an optional `effect` adds the effect's direction/magnitude/duration,
     derived with the same `skill-effect-display` helpers the chips/skill tooltip use. Degrades
     gracefully: the name falls back to the humanised enum name and the type/description sections drop
     out when the reference data (or its metadata) is unavailable. -->
<TooltipShell {accent} hidden={attributeId == null} bind:base={container}>
	{#snippet header()}
		<TooltipTitle label={typeLabel} name={displayName} diamondColor={accent} labelColor={accent}>
			{#snippet leading()}
				{#if attributeId != null}
					<AttributeIcon id={attributeId} size={30} />
				{/if}
			{/snippet}
			{#snippet trailing()}
				{#if effectDetail?.pill}
					<DurationPill
						progress={effectDetail.pill.progress}
						text={effectDetail.pill.text}
						color={effectDetail.color}
					/>
				{/if}
			{/snippet}
		</TooltipTitle>
	{/snippet}

	{#if description}
		<TooltipSection label="Description" last={!effectDetail}>
			<p class="at-desc">{description}</p>
		</TooltipSection>
	{/if}

	{#if effectDetail}
		<TooltipSection label="Active effect" last>
			<div class="at-effect" data-testid="attr-tip-effect">
				<span class="at-effect-mag" style:color={effectDetail.color}>{effectDetail.magnitude}</span>
				<span class="at-effect-dir" style:color={effectDetail.color}>{effectDetail.label}</span>
			</div>
			{#if effectDetail.sourceName}
				<div class="at-effect-source">
					<span class="at-effect-source-label">Source</span>
					<span class="at-effect-source-name">{effectDetail.sourceName}</span>
				</div>
			{/if}
		</TooltipSection>
	{/if}
</TooltipShell>

<script lang="ts">
import type { EAttribute } from '$lib/api';
import {
	attributeColor,
	attributeIsHarmful,
	attributeName,
	attributeTypeName,
	effectDirection,
	effectDirectionColor,
	formatEffectMagnitude
} from '$lib/common';
import { staticData } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
import TooltipShell from './TooltipShell.svelte';
import TooltipSection from './TooltipSection.svelte';
import TooltipTitle from './TooltipTitle.svelte';
import DurationPill from './DurationPill.svelte';
import type { AttributeEffectContext } from './attribute-tooltip.svelte';

export const getBaseNode = () => container;

interface Props {
	/** The attribute to describe, or `undefined` for the empty/hidden panel. */
	attributeId: EAttribute | undefined;
	/** Effect-chip detail (direction/magnitude/duration), shown only in the combat-chip context. */
	effect?: AttributeEffectContext;
}

const { attributeId, effect }: Props = $props();

// Bound to the shell's root element and relocated into the global tooltip container by
// getBaseNode(); reactive so the relocation runs once the shell has mounted.
let container = $state<HTMLDivElement>();

const attribute = $derived(attributeId != null ? staticData.attributes?.find((a) => a.id === attributeId) : undefined);
const displayName = $derived(attributeId != null ? attributeName(attributeId, staticData.attributes) : '');
const typeLabel = $derived(attributeTypeName(attribute?.attributeType));
const description = $derived(attribute?.description ?? '');
const accent = $derived(attributeId != null ? attributeColor(attributeId) : 'var(--text-secondary)');

// The buff/debuff framing of the chip's effect, reusing the shared skill-effect helpers so the
// wording/direction match the chips and the skill tooltip's "On hit" lines. When the caller supplies
// a live `remainingMs` (the combat chips do) it also drives a depleting countdown pill.
const effectDetail = $derived.by(() => {
	if (!effect || attributeId == null) {
		return undefined;
	}
	const direction = effectDirection(
		attributeIsHarmful(attributeId, staticData.attributes),
		effect.modifierType,
		effect.amount
	);
	const remainingMs = effect.remainingMs;
	let pill: { progress: number; text: string } | undefined;
	if (remainingMs != null && effect.durationMs > 0) {
		pill = {
			progress: Math.max(0, Math.min(100, (remainingMs / effect.durationMs) * 100)),
			text: `${(remainingMs / 1000).toFixed(1)}s`
		};
	}
	return {
		label: direction === 'buff' ? 'Buff' : 'Debuff',
		color: effectDirectionColor(direction),
		magnitude: formatEffectMagnitude(effect.modifierType, effect.amount),
		sourceName: effect.sourceName,
		pill
	};
});
</script>

<style lang="scss">
.at-desc {
	margin: 0;
	font-size: 12px;
	line-height: 1.5;
	color: var(--text-tertiary);
}

.at-effect {
	display: flex;
	align-items: baseline;
	gap: 8px;
}

.at-effect-mag {
	font-family: var(--mono);
	font-size: 12px;
	font-weight: 600;
}

.at-effect-dir {
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
}

.at-effect-source {
	display: flex;
	align-items: baseline;
	gap: 8px;
	margin-top: 6px;
}

.at-effect-source-label {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.at-effect-source-name {
	font-size: 12px;
	color: var(--text-secondary);
}
</style>
