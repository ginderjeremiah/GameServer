<!-- The row of active-effect chips on a battler card: one chip per timed effect currently modifying the
     battler, tinted by buff/debuff direction, with a render-interpolated countdown bar that depletes
     smoothly between ticks and visibly refills when the effect is refreshed (re-applied). Reads the
     battler's reactive `activeEffects` projection, so chips appear/expire as effects come and go. -->
{#if battler.activeEffects.length > 0}
	<div class="effect-chips" class:reversed data-testid="effect-chips">
		{#each battler.activeEffects as effect (effect.sourceId)}
			{@const color = effectDirectionColor(
				effectDirection(attributeIsHarmful(effect.attribute, staticData.attributes), effect.modifierType, effect.amount)
			)}
			<!-- svelte-ignore a11y_no_static_element_interactions -->
			<div
				class="effect-chip"
				style:--chip-accent={color}
				onmouseenter={(ev) => showChipTooltip(effect, ev)}
				onmousemove={(ev) => tip.controller.move(ev)}
				onmouseleave={() => tip.controller.hide()}
			>
				<AttributeIcon id={effect.attribute} size={13} />
				<span class="chip-mag">{formatEffectMagnitude(effect.modifierType, effect.amount)}</span>
				<span class="chip-attr">{attributeName(effect.attribute, staticData.attributes)}</span>
				<span class="chip-time">{remainingSeconds(effect)}s</span>
				<div class="chip-track">
					<div class="chip-fill" style:width="{remainingPercent(effect)}%"></div>
				</div>
			</div>
		{/each}
	</div>
{/if}

<!-- One tooltip instance per chip row, anchored to whichever chip is hovered. Always mounted (it
     stays hidden until a chip is hovered) so its registration survives chips coming and going. -->
<AttributeTooltip bind:this={tooltip} attributeId={tip.attributeId} effect={tip.effect} />

<script lang="ts">
import {
	attributeIsHarmful,
	attributeName,
	effectDirection,
	effectDirectionColor,
	formatEffectMagnitude
} from '$lib/common';
import { staticData, type TooltipComponent } from '$stores';
import AttributeIcon from '$components/AttributeIcon.svelte';
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

const remainingSeconds = (effect: ActiveEffectView) => (effect.renderRemainingMs / 1000).toFixed(2);
const remainingPercent = (effect: ActiveEffectView) =>
	effect.durationMs > 0 ? Math.max(0, Math.min(100, (effect.renderRemainingMs / effect.durationMs) * 100)) : 0;
const showChipTooltip = (effect: ActiveEffectView, ev: MouseEvent) =>
	tip.controller.show(effect.attribute, ev, {
		modifierType: effect.modifierType,
		amount: effect.amount,
		durationMs: effect.durationMs
	});
</script>

<style lang="scss">
.effect-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;
	margin-bottom: 12px;

	&.reversed {
		justify-content: flex-end;
	}
}

.effect-chip {
	position: relative;
	display: flex;
	align-items: baseline;
	gap: 5px;
	padding: 3px 7px 5px;
	border: 1px solid color-mix(in srgb, var(--chip-accent) 45%, transparent);
	border-radius: 2px;
	background: color-mix(in srgb, var(--chip-accent) 10%, transparent);
	overflow: hidden;

	:global(.attr-icon) {
		align-self: center;
	}
}

.chip-mag {
	font-family: var(--mono);
	font-size: 10px;
	font-weight: 600;
	color: var(--chip-accent);
}

.chip-attr {
	font-size: 10px;
	color: var(--text-secondary);
	white-space: nowrap;
}

.chip-time {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
}

.chip-track {
	position: absolute;
	left: 0;
	right: 0;
	bottom: 0;
	height: 2px;
	background: color-mix(in srgb, var(--white) 8%, transparent);
}

.chip-fill {
	height: 100%;
	background: var(--chip-accent);
}
</style>
