import { focusAnchor, type TooltipAnchor } from '$stores/tooltip.svelte';
import type { TierView } from './proficiencies-lexicon';

/**
 * Imperative controller for the single shared {@link WordOfPowerTooltip}: opens it for a hovered/focused
 * tier, repositions it as the cursor moves, and hides it. The Proficiencies screen owns the panel and
 * builds this controller (mirroring the challenges reward tooltip), then passes it down to the spine's
 * tier cards. `show`/`move` accept a {@link TooltipAnchor} so a card is reachable by mouse and keyboard.
 */
export interface WordTooltipController {
	/** Stable DOM id of the shared panel, for wiring a focusable card's `aria-describedby`. */
	readonly describedById: string;
	show: (tier: TierView, anchor: TooltipAnchor) => void;
	move: (anchor: TooltipAnchor) => void;
	hide: () => void;
}

export interface WordHoverParams {
	controller: WordTooltipController;
	tier: TierView;
}

/**
 * Opens the shared word-of-power tooltip for {@link WordHoverParams.tier} while the element is hovered
 * or focused — anchored at the cursor on hover, off the element's box on keyboard focus — and hides it
 * on leave/blur. A mouse click is left to the hover handlers (which already track the cursor) so the
 * tooltip doesn't jump on click (mirrors `attributeHover`).
 */
export function wordHover(node: HTMLElement, params: WordHoverParams) {
	let { controller, tier } = params;

	const onEnter = (e: MouseEvent) => controller.show(tier, e);
	const onMove = (e: MouseEvent) => controller.move(e);
	const onLeave = () => controller.hide();
	const onFocus = (e: FocusEvent) => {
		const anchor = focusAnchor(e);
		if (anchor) {
			controller.show(tier, anchor);
		}
	};
	const onBlur = () => controller.hide();

	node.addEventListener('mouseenter', onEnter);
	node.addEventListener('mousemove', onMove);
	node.addEventListener('mouseleave', onLeave);
	node.addEventListener('focus', onFocus);
	node.addEventListener('blur', onBlur);

	return {
		update(next: WordHoverParams) {
			({ controller, tier } = next);
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
