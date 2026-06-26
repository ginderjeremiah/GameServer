import { focusAnchor, type TooltipAnchor } from '$stores/tooltip.svelte';

/**
 * The imperative controller {@link tooltipHover} drives for a single shared tooltip: open it for a
 * payload, reposition it as the cursor moves, and hide it. Generic over the payload `P` so one action
 * serves any tooltip surface (attribute pills, words of power, …); a concrete controller may carry extra
 * members (e.g. a `describedById`) and stays structurally assignable.
 */
export interface TooltipHoverController<P> {
	show: (payload: P, anchor: TooltipAnchor) => void;
	move: (anchor: TooltipAnchor) => void;
	hide: () => void;
}

/**
 * A {@link TooltipHoverController} for a single shared panel that also exposes that panel's stable DOM
 * id, for wiring a focusable trigger's `aria-describedby` (via the `describedByTooltip` action). The
 * common shape of the concrete per-surface controllers (attribute pills, words of power).
 */
export interface DescribedTooltipController<P> extends TooltipHoverController<P> {
	/** Stable DOM id of the shared panel, for wiring a focusable trigger's `aria-describedby`. */
	readonly describedById: string;
}

export interface TooltipHoverParams<P> {
	/** The shared tooltip controller, or `undefined` to no-op (a screen may not provide one). */
	controller: TooltipHoverController<P> | undefined;
	/** The value handed to `controller.show` while the element is hovered or focused. */
	payload: P;
}

/**
 * Opens the shared tooltip for {@link TooltipHoverParams.payload} while the element is hovered or
 * focused — anchored at the cursor on hover and off the element's box on keyboard focus — and hides it
 * on leave/blur, replacing the per-trigger mouse/focus-handler boilerplate. The focus handler is gated
 * through `focusAnchor` so a mouse-click focus doesn't re-anchor the tooltip off the box (which would
 * make it jump away from the cursor the hover handlers already track — #880). The controller is passed
 * in rather than read from context because an action runs after init and can't call `getContext`.
 */
export function tooltipHover<P>(node: HTMLElement, params: TooltipHoverParams<P>) {
	let { controller, payload } = params;

	const onEnter = (e: MouseEvent) => controller?.show(payload, e);
	const onMove = (e: MouseEvent) => controller?.move(e);
	const onLeave = () => controller?.hide();
	const onFocus = (e: FocusEvent) => {
		const anchor = focusAnchor(e);
		if (anchor) {
			controller?.show(payload, anchor);
		}
	};
	const onBlur = () => controller?.hide();

	node.addEventListener('mouseenter', onEnter);
	node.addEventListener('mousemove', onMove);
	node.addEventListener('mouseleave', onLeave);
	node.addEventListener('focus', onFocus);
	node.addEventListener('blur', onBlur);

	return {
		update(next: TooltipHoverParams<P>) {
			({ controller, payload } = next);
		},
		destroy() {
			node.removeEventListener('mouseenter', onEnter);
			node.removeEventListener('mousemove', onMove);
			node.removeEventListener('mouseleave', onLeave);
			node.removeEventListener('focus', onFocus);
			node.removeEventListener('blur', onBlur);
		}
	};
}
