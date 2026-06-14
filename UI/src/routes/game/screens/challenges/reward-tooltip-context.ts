import { getContext, setContext } from 'svelte';
import type { Position } from '$stores';
import type { ResolvedReward } from './challenges-view.svelte';

/**
 * Cross-cutting controller for the single reward tooltip, registered once by the
 * Challenges screen and consumed by every (deeply nested) reward affordance via
 * context — so we don't thread hover handlers through the rail/overview/detail
 * component tree. Mirrors how the inventory grid drives the shared tooltip, but
 * shared through context rather than props.
 *
 * `show`/`move` accept either a pointer event (positioned at the cursor) or a
 * focused element (positioned at the element's box), so the same affordance is
 * reachable by keyboard/screen reader as well as by mouse.
 */
export type RewardAnchor = MouseEvent | HTMLElement;

export interface RewardTooltipController {
	show: (reward: ResolvedReward, anchor: RewardAnchor) => void;
	move: (anchor: RewardAnchor) => void;
	hide: () => void;
}

/** Resolve the tooltip position from a cursor (pointer) or an element's box (focus). */
export const anchorPosition = (anchor: RewardAnchor): Position => {
	if (anchor instanceof HTMLElement) {
		const rect = anchor.getBoundingClientRect();
		return { x: rect.left + rect.width / 2, y: rect.bottom };
	}
	return { x: anchor.clientX, y: anchor.clientY };
};

const REWARD_TOOLTIP_KEY = Symbol('reward-tooltip');

export const setRewardTooltip = (controller: RewardTooltipController): void => {
	setContext(REWARD_TOOLTIP_KEY, controller);
};

export const getRewardTooltip = (): RewardTooltipController | undefined =>
	getContext<RewardTooltipController>(REWARD_TOOLTIP_KEY);
