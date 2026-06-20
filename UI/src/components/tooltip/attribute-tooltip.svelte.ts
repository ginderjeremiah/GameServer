import { getContext, setContext } from 'svelte';
import type { EAttribute, EModifierType } from '$lib/api';
import {
	anchorPosition,
	registerTooltipComponent,
	type TooltipAnchor,
	type TooltipComponent
} from '$stores/tooltip.svelte';

/** One active application within a stacked effect, for the tooltip's per-source breakdown. */
export interface AttributeEffectApplication {
	/** This application's own magnitude — applications can differ when caster-scaling shifts between fires. */
	amount: number;
	/** Display name of the skill that applied this application, when it can be resolved. */
	sourceName?: string;
}

/**
 * The effect detail shown when an {@link AttributeTooltip} is opened from a combat effect chip: the
 * modifier that produced the chip, from which the tooltip derives the effect's direction and
 * combined magnitude (via the shared `skill-effect-display` helpers) and — from the live `remainingMs`
 * / `durationMs` — a depleting countdown pill. All applications on an attribute share one expiry, so
 * the pill is the whole stack's single countdown; when more than one application is active the panel
 * also breaks down the individual applications (their amounts and sources). Omitted on the non-combat
 * attribute surfaces (breakdown rail, point-buy steppers, skill scaling chips), which show only the
 * attribute.
 */
export interface AttributeEffectContext {
	modifierType: EModifierType;
	/** Combined magnitude of all active applications (additive summed, multiplicative compounded). */
	amount: number;
	durationMs: number;
	/** Live shared remaining time of the stack, driving the header countdown pill. Omit for a
	 *  non-timed/static context. */
	remainingMs?: number;
	/** Display name of the single source skill — set only when exactly one application is active (the
	 *  multi-application case names each source per breakdown row instead). */
	sourceName?: string;
	/** Each active application, shown as a per-source breakdown when more than one is stacked. */
	applications?: AttributeEffectApplication[];
}

/**
 * Imperative controller for a single shared `AttributeTooltip` — opens it for a given attribute,
 * repositions it as the cursor moves, and hides it. Shared down a screen via {@link setAttributeTooltip}
 * context so nested attribute surfaces don't have to thread hover handlers, mirroring the
 * reward-tooltip controller. The combat chips additionally feed the panel a live effect context
 * directly (so the countdown pill stays live); the controller only governs which attribute is shown
 * and where.
 *
 * `show`/`move` accept a {@link TooltipAnchor} — a pointer event (positioned at the cursor) or a
 * focused element (positioned off its box) — so a trigger is reachable by mouse and keyboard alike.
 */
export interface AttributeTooltipController {
	/** Stable DOM id of the shared panel, for wiring a focusable trigger's `aria-describedby`. */
	readonly describedById: string;
	show: (attributeId: EAttribute, anchor: TooltipAnchor) => void;
	move: (anchor: TooltipAnchor) => void;
	hide: () => void;
}

/** The reactive render state of the tooltip plus its {@link AttributeTooltipController}. */
export interface AttributeTooltipHandle {
	readonly controller: AttributeTooltipController;
	readonly attributeId: EAttribute | undefined;
}

/**
 * Registers a single attribute tooltip with the global tooltip store and returns the reactive render
 * state (the hovered `attributeId`) plus an imperative {@link AttributeTooltipController}. The caller
 * renders `<AttributeTooltip bind:this={component} attributeId={handle.attributeId} />` and drives it
 * from hover/focus handlers — directly when the triggers are local (combat chips, skill scaling
 * chips), or via {@link setAttributeTooltip} context when they live in descendant components (the
 * breakdown rail rows, the point-buy steppers). The combat chips pass a live `effect` to the panel
 * separately, so its countdown pill keeps depleting between renders.
 *
 * Must be called during component initialisation (it registers an `onDestroy` cleanup).
 */
export function createAttributeTooltip(getComponent: () => TooltipComponent | undefined): AttributeTooltipHandle {
	let attributeId = $state<EAttribute | undefined>();
	const { describedById, setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(getComponent);

	const controller: AttributeTooltipController = {
		describedById,
		show(id, anchor) {
			attributeId = id;
			setTooltipPosition(anchorPosition(anchor));
			showTooltip();
		},
		move(anchor) {
			setTooltipPosition(anchorPosition(anchor));
		},
		hide() {
			attributeId = undefined;
			hideTooltip();
		}
	};

	return {
		controller,
		get attributeId() {
			return attributeId;
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
