<!-- The row of active-effect chips on a battler card: one skill-style icon tile per (attribute, modifier
     type) currently modifying the battler. Effects STACK — re-applying one adds another application, and
     all applications on an attribute share a single expiry (#992) — so each tile groups every active
     application of one attribute+type: it shows the attribute's icon with a radial (conic) cooldown
     overlay tracking the shared remaining, the combined-total magnitude badge, a buff/debuff direction
     tint, and (when more than one is active) a count badge in the top-right corner. Reads the battler's
     reactive `activeEffects` projection, so tiles appear/expire as effects come and go. -->
{#if battler.activeEffects.length > 0}
	<div class="effect-chips" class:reversed data-testid="effect-chips">
		{#each battler.activeEffects as effect (effectKey(effect))}
			{@const direction = effectDirection(
				attributeIsHarmful(effect.attribute, staticData.attributes),
				effect.modifierType,
				effect.totalAmount
			)}
			{@const magnitude = formatEffectMagnitude(effect.modifierType, effect.totalAmount)}
			<!-- A focusable button (mirroring the skill slots) so the attribute tooltip is reachable by
			     keyboard and screen reader, not just on hover. Its accessible name describes the effect. -->
			<button
				type="button"
				class="effect-chip"
				style:--chip-accent={effectDirectionColor(direction)}
				aria-label="{attributeName(effect.attribute, staticData.attributes)} {direction}, {magnitude}{effect.count > 1
					? `, ${effect.count} applications`
					: ''}"
				onmouseenter={(ev) => showChipTooltip(effect, ev)}
				onmousemove={(ev) => tip.controller.move(ev)}
				onmouseleave={hideChipTooltip}
				onfocus={(ev) => focusChipTooltip(effect, ev)}
				onblur={hideChipTooltip}
				use:describedByTooltip={tip.controller.describedById}
			>
				<AttributeIcon id={effect.attribute} size={26} />
				<CooldownOverlay sweep={remainingSweep(effect)} />
				{#if effect.count > 1}
					<span class="chip-count" data-testid="chip-count">{effect.count}</span>
				{/if}
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
import { focusAnchor, type TooltipAnchor } from '$stores/tooltip.svelte';
import AttributeIcon from '$components/AttributeIcon.svelte';
import CooldownOverlay from '$components/CooldownOverlay.svelte';
import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';
import { createAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import type { ActiveEffectView, Battler } from '$lib/battle';

type Props = {
	battler: Battler;
	/** Right-aligns the row to match the enemy/boss card's mirrored layout. */
	reversed?: boolean;
};

const { battler, reversed = false }: Props = $props();

// The battler folds stacked applications into one active-effect view per (attribute, modifier type), so
// each view is already exactly one chip — its stable key.
const effectKey = (effect: ActiveEffectView): string => `${effect.attribute}:${effect.modifierType}`;

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);

// The key of the chip the tooltip is currently anchored to (if any). Tracked so the live view can be
// re-resolved each tick and so an expiring chip can close the tooltip itself — a chip removed under the
// cursor never fires `mouseleave`, which otherwise left the panel stuck open.
let shownKey = $state<string>();
const shownEffect = $derived(
	shownKey != null ? battler.activeEffects.find((e) => effectKey(e) === shownKey) : undefined
);

// The skill an active effect came from: its `sourceId` is the authored skill-effect id, so look up
// the skill that owns it. Display-only (never touches battle math); undefined for a retired/unknown
// skill. Skill→effect ownership is static reference data, so the effectId→name index is memoised off
// the `staticData.skills` reference and rebuilt only when that catalogue changes — rather than
// rescanning every skill's effects per call (this resolves inside the per-frame `shownEffectContext`).
let skillNameByEffectIdCache: { skills: unknown; map: Map<number, string> } | undefined;
const skillNameByEffectId = (): Map<number, string> => {
	const skills = staticData.skills;
	if (!skillNameByEffectIdCache || skillNameByEffectIdCache.skills !== skills) {
		// A plain memo cache, not reactive state — it never drives rendering, so SvelteMap is unwanted.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const map = new Map<number, string>();
		for (const skill of skills ?? []) {
			if (!skill) {
				continue;
			}
			for (const effect of skill.effects) {
				map.set(effect.id, skill.name);
			}
		}
		skillNameByEffectIdCache = { skills, map };
	}
	return skillNameByEffectIdCache.map;
};

const sourceSkillName = (sourceId: number): string | undefined => skillNameByEffectId().get(sourceId);

// Live effect context for the panel: reads renderRemainingMs so the countdown pill depletes smoothly. A
// single application names its source in the header; a stack names each contributing source per row.
const shownEffectContext = $derived(
	shownEffect
		? {
				modifierType: shownEffect.modifierType,
				amount: shownEffect.totalAmount,
				count: shownEffect.count,
				durationMs: shownEffect.durationMs,
				remainingMs: shownEffect.renderRemainingMs,
				sourceName: shownEffect.count === 1 ? sourceSkillName(shownEffect.sources[0].sourceId) : undefined,
				sources:
					shownEffect.count > 1
						? shownEffect.sources.map((s) => ({
								amount: s.amount,
								sourceName: sourceSkillName(s.sourceId),
								count: s.count
							}))
						: undefined
			}
		: undefined
);

// Close the tooltip when the chip it's anchored to expires under the cursor (no `mouseleave` fires).
$effect(() => {
	if (shownKey != null && !shownEffect) {
		hideChipTooltip();
	}
});

// The transparent (revealed) arc of the radial overlay: the render-interpolated shared remaining as a
// fraction of the most-recent application's duration, in degrees. Depletes toward 0 as the stack
// expires; jumps back toward 360 when a fresh application resets the shared `renderRemainingMs`.
// Note: the remaining is shared per *attribute* but this denominator is the (attribute, modifierType)
// view's own latest duration. On the rare attribute carrying both an additive and a multiplicative
// effect, a reset driven by the other type can leave this view's sweep below full rather than at 360°
// — intended (display-only); don't "fix" it by widening the share to the attribute.
const remainingSweep = (effect: ActiveEffectView) =>
	effect.durationMs > 0 ? Math.max(0, Math.min(360, (effect.renderRemainingMs / effect.durationMs) * 360)) : 0;

const showChipTooltip = (effect: ActiveEffectView, anchor: TooltipAnchor) => {
	shownKey = effectKey(effect);
	tip.controller.show(effect.attribute, anchor);
};
// Keyboard focus anchors off the chip's box; a mouse click is left to the hover handlers so the
// tooltip keeps tracking the cursor instead of jumping (#880).
const focusChipTooltip = (effect: ActiveEffectView, ev: FocusEvent) => {
	const anchor = focusAnchor(ev);
	if (anchor) {
		showChipTooltip(effect, anchor);
	}
};
const hideChipTooltip = () => {
	shownKey = undefined;
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

// Stack count, pinned to the top-right corner above the overlay so it stays legible as the wedge
// sweeps in. Shown only when more than one application is active.
.chip-count {
	position: absolute;
	top: 0;
	right: 0;
	min-width: 12px;
	padding: 0 2px;
	font-family: var(--mono);
	font-size: 9px;
	font-weight: 700;
	line-height: 12px;
	text-align: center;
	color: var(--chip-accent);
	background: color-mix(in srgb, var(--black) 60%, transparent);
	border-bottom-left-radius: 3px;
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
