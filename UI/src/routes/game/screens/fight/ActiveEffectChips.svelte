<!-- The row of active-effect chips on a battler card: one chip per timed effect currently modifying the
     battler, tinted by buff/debuff direction, with a render-interpolated countdown bar that depletes
     smoothly between ticks and visibly refills when the effect is refreshed (re-applied). Reads the
     battler's reactive `activeEffects` projection, so chips appear/expire as effects come and go. -->
{#if battler.activeEffects.length > 0}
	<div class="effect-chips" class:reversed data-testid="effect-chips">
		{#each battler.activeEffects as effect (effect.sourceId)}
			{@const color = effectDirectionColor(effectDirection(effect.attribute, effect.modifierType, effect.amount))}
			<div class="effect-chip" style:--chip-accent={color} title={chipTitle(effect)}>
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

<script lang="ts">
import { attributeName, effectDirection, effectDirectionColor, formatEffectMagnitude } from '$lib/common';
import { staticData } from '$stores';
import type { ActiveEffectView, Battler } from '$lib/battle';

type Props = {
	battler: Battler;
	/** Right-aligns the row to match the enemy/boss card's mirrored layout. */
	reversed?: boolean;
};

const { battler, reversed = false }: Props = $props();

const remainingSeconds = (effect: ActiveEffectView) => (effect.renderRemainingMs / 1000).toFixed(2);
const remainingPercent = (effect: ActiveEffectView) =>
	effect.durationMs > 0 ? Math.max(0, Math.min(100, (effect.renderRemainingMs / effect.durationMs) * 100)) : 0;
const chipTitle = (effect: ActiveEffectView) =>
	`${formatEffectMagnitude(effect.modifierType, effect.amount)} ${attributeName(effect.attribute, staticData.attributes)}`;
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
