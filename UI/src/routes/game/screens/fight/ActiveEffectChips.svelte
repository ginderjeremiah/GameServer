<!-- The row of active-effect chips on a battler card: one skill-style icon tile per timed effect
     currently modifying the battler. Each tile shows the attribute's icon with a radial (conic)
     cooldown overlay that depletes as the effect's remaining duration ticks down and refills when the
     effect is refreshed (re-applied), a small magnitude badge, and a buff/debuff direction tint. Reads
     the battler's reactive `activeEffects` projection, so tiles appear/expire as effects come and go. -->
{#if battler.activeEffects.length > 0}
	<div class="effect-chips" class:reversed data-testid="effect-chips">
		{#each battler.activeEffects as effect (effect.sourceId)}
			{@const direction = effectDirection(
				attributeIsHarmful(effect.attribute, staticData.attributes),
				effect.modifierType,
				effect.amount
			)}
			{@const magnitude = formatEffectMagnitude(effect.modifierType, effect.amount)}
			<!-- A focusable button (mirroring the skill slots) so the attribute tooltip is reachable by
			     keyboard and screen reader, not just on hover. Its accessible name describes the effect. -->
			<button
				type="button"
				class="effect-chip"
				style:--chip-accent={effectDirectionColor(direction)}
				aria-label="{attributeName(effect.attribute, staticData.attributes)} {direction}, {magnitude}"
				onmouseenter={(ev) => showChipTooltip(effect, ev)}
				onmousemove={(ev) => tip.controller.move(ev)}
				onmouseleave={hideChipTooltip}
				onfocus={(ev) => showChipTooltip(effect, ev.currentTarget)}
				onblur={hideChipTooltip}
			>
				<AttributeIcon id={effect.attribute} size={26} />
				<CooldownOverlay sweep={remainingSweep(effect)} />
				<span class="chip-mag">{magnitude}</span>
			</button>
		{/each}
	</div>
{/if}

<!-- One tooltip instance per chip row, anchored to whichever chip is hovered/focused. Always mounted
     (it stays hidden until a chip is triggered) so its registration survives chips coming and going.
     The effect context is the live `ActiveEffectView` (resolved by source id), so the tooltip's
     countdown pill keeps depleting and the panel closes itself if the hovered effect expires. -->
<AttributeTooltip bind:this={tooltip} attributeId={tip.attributeId} effect={shownEffectContext} />

<script lang="ts">
import {
	attributeIsHarmful,
	attributeName,
	effectDirection,
	effectDirectionColor,
	formatEffectMagnitude
} from '$lib/common';
import { staticData, type TooltipComponent } from '$stores';
import { type TooltipAnchor } from '$stores/tooltip.svelte';
import AttributeIcon from '$components/AttributeIcon.svelte';
import CooldownOverlay from '$components/CooldownOverlay.svelte';
import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';
import { createAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import type { ActiveEffectView, Battler } from '$lib/battle';

type Props = {
	battler: Battler;
	/** Right-aligns the row to match the enemy/boss card's mirrored layout. */
	reversed?: boolean;
};

const { battler, reversed = false }: Props = $props();

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);

// The source id of the chip the tooltip is currently anchored to (if any). Tracked so the live
// effect can be re-resolved each tick and so an expiring chip can close the tooltip itself — a chip
// removed under the cursor never fires `mouseleave`, which otherwise left the panel stuck open.
let shownSourceId = $state<number>();
const shownEffect = $derived(
	shownSourceId != null ? battler.activeEffects.find((e) => e.sourceId === shownSourceId) : undefined
);

// The skill an active effect came from: its `sourceId` is the authored skill-effect id, so find the
// skill that owns it. Display-only (never touches battle math); undefined for a retired/unknown skill.
const sourceSkillName = (sourceId: number): string | undefined =>
	staticData.skills?.find((skill) => skill?.effects.some((e) => e.id === sourceId))?.name;

// Live effect context for the panel: reads renderRemainingMs so the countdown pill depletes.
const shownEffectContext = $derived(
	shownEffect
		? {
				modifierType: shownEffect.modifierType,
				amount: shownEffect.amount,
				durationMs: shownEffect.durationMs,
				remainingMs: shownEffect.renderRemainingMs,
				sourceName: sourceSkillName(shownEffect.sourceId)
			}
		: undefined
);

// Close the tooltip when the chip it's anchored to expires under the cursor (no `mouseleave` fires).
$effect(() => {
	if (shownSourceId != null && !shownEffect) {
		hideChipTooltip();
	}
});

// The transparent (revealed) arc of the radial overlay: the render-interpolated remaining fraction of
// the effect's duration, in degrees. Depletes toward 0 as the effect expires; resets to 360 (the icon
// fully revealed) when the effect is refreshed, since refresh restores `renderRemainingMs`.
const remainingSweep = (effect: ActiveEffectView) =>
	effect.durationMs > 0 ? Math.max(0, Math.min(360, (effect.renderRemainingMs / effect.durationMs) * 360)) : 0;

const showChipTooltip = (effect: ActiveEffectView, anchor: TooltipAnchor) => {
	shownSourceId = effect.sourceId;
	tip.controller.show(effect.attribute, anchor);
};
const hideChipTooltip = () => {
	shownSourceId = undefined;
	tip.controller.hide();
};
</script>

<style lang="scss">
.effect-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;

	&.reversed {
		justify-content: flex-end;
	}
}

.effect-chip {
	// Reset native button chrome (the chip is a <button> so its tooltip is keyboard-reachable),
	// then lay out as a square icon tile mirroring the skill slots.
	appearance: none;
	margin: 0;
	padding: 0;
	font: inherit;
	color: inherit;
	position: relative;
	width: 38px;
	height: 38px;
	display: flex;
	align-items: center;
	justify-content: center;
	border: 1px solid color-mix(in srgb, var(--chip-accent) 45%, transparent);
	border-radius: 2px;
	background: color-mix(in srgb, var(--chip-accent) 10%, transparent);
	overflow: hidden;
	cursor: default;
	transition: border-color 140ms;

	&:focus-visible {
		outline: 2px solid var(--chip-accent);
		outline-offset: 2px;
	}

	:global(.attr-icon) {
		opacity: 0.92;
	}
}

// Sits last in the DOM (above the overlay) so the magnitude stays legible as the wedge sweeps in.
.chip-mag {
	position: absolute;
	left: 0;
	right: 0;
	bottom: 0;
	padding: 1px 0;
	font-family: var(--mono);
	font-size: 9px;
	font-weight: 600;
	line-height: 1;
	text-align: center;
	color: var(--chip-accent);
	background: color-mix(in srgb, var(--black) 55%, transparent);
}
</style>
