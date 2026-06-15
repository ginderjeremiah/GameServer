import { getContext, setContext } from 'svelte';
import type { EAttribute, EModifierType } from '$lib/api';
import {
	anchorPosition,
	registerTooltipComponent,
	type TooltipAnchor,
	type TooltipComponent
} from '$stores/tooltip.svelte';

/**
 * The effect detail shown when an {@link AttributeTooltip} is opened from a combat effect chip: the
 * modifier that produced the chip, from which the tooltip derives the effect's direction, magnitude
 * and duration via the shared `skill-effect-display` helpers. Omitted on the non-combat attribute
 * surfaces (breakdown rail, point-buy steppers, skill scaling chips), which show only the attribute.
 */
export interface AttributeEffectContext {
	modifierType: EModifierType;
	amount: number;
	durationMs: number;
}

/**
 * Imperative controller for a single shared `AttributeTooltip` — opens it for a given attribute
 * (optionally with effect-chip detail), repositions it as the cursor moves, and hides it. Shared
 * down a screen via {@link setAttributeTooltip} context so nested attribute surfaces don't have to
 * thread hover handlers, mirroring the reward-tooltip controller.
 *
 * `show`/`move` accept a {@link TooltipAnchor} — a pointer event (positioned at the cursor) or a
 * focused element (positioned off its box) — so a trigger is reachable by mouse and keyboard alike.
 */
export interface AttributeTooltipController {
	show: (attributeId: EAttribute, anchor: TooltipAnchor, effect?: AttributeEffectContext) => void;
	move: (anchor: TooltipAnchor) => void;
	hide: () => void;
}

/** The reactive render state of the tooltip plus its {@link AttributeTooltipController}. */
export interface AttributeTooltipHandle {
	readonly controller: AttributeTooltipController;
	readonly attributeId: EAttribute | undefined;
	readonly effect: AttributeEffectContext | undefined;
}

/**
 * Registers a single attribute tooltip with the global tooltip store and returns the reactive render
 * state (the hovered `attributeId` / `effect`) plus an imperative {@link AttributeTooltipController}.
 * The caller renders `<AttributeTooltip bind:this={component} attributeId={handle.attributeId}
 * effect={handle.effect} />` and drives it from hover/focus handlers — directly when the triggers are
 * local (combat chips, skill scaling chips), or via {@link setAttributeTooltip} context when they live
 * in descendant components (the breakdown rail rows, the point-buy steppers).
 *
 * Must be called during component initialisation (it registers an `onDestroy` cleanup).
 */
export function createAttributeTooltip(getComponent: () => TooltipComponent | undefined): AttributeTooltipHandle {
	let attributeId = $state<EAttribute | undefined>();
	let effect = $state<AttributeEffectContext | undefined>();
	const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(getComponent);

	const controller: AttributeTooltipController = {
		show(id, anchor, fx) {
			attributeId = id;
			effect = fx;
			setTooltipPosition(anchorPosition(anchor));
			showTooltip();
		},
		move(anchor) {
			setTooltipPosition(anchorPosition(anchor));
		},
		hide() {
			attributeId = undefined;
			effect = undefined;
			hideTooltip();
		}
	};

	return {
		controller,
		get attributeId() {
			return attributeId;
		},
		get effect() {
			return effect;
		}
	};
}

const ATTRIBUTE_TOOLTIP_KEY = Symbol('attribute-tooltip');

/** Publish a controller to descendant attribute surfaces (call from the screen that owns the panel). */
export const setAttributeTooltip = (controller: AttributeTooltipController): void => {
	setContext(ATTRIBUTE_TOOLTIP_KEY, controller);
};

/** The nearest ancestor's attribute-tooltip controller, or `undefined` if no screen provides one. */
export const getAttributeTooltip = (): AttributeTooltipController | undefined =>
	getContext<AttributeTooltipController>(ATTRIBUTE_TOOLTIP_KEY);
