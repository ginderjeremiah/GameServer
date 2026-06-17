import { getContext, setContext } from 'svelte';
import type { TooltipAnchor } from '$stores';
import type { ResolvedReward } from './challenges-view.svelte';

/**
 * Cross-cutting controller for the single reward tooltip, registered once by the
 * Challenges screen and consumed by every (deeply nested) reward affordance via
 * context — so we don't thread hover handlers through the rail/overview/detail
 * component tree. Mirrors how the inventory grid drives the shared tooltip, but
 * shared through context rather than props.
 *
 * `show`/`move` accept a {@link TooltipAnchor} — either a pointer event (positioned
 * at the cursor) or a focused element (positioned off its box) — so the same
 * affordance is reachable by keyboard/screen reader as well as by mouse.
 */
export interface RewardTooltipController {
	/** Stable DOM id of the shared panel, for wiring a focusable trigger's `aria-describedby`. */
	readonly describedById: string;
	show: (reward: ResolvedReward, anchor: TooltipAnchor) => void;
	move: (anchor: TooltipAnchor) => void;
	hide: () => void;
}

const REWARD_TOOLTIP_KEY = Symbol('reward-tooltip');

export const setRewardTooltip = (controller: RewardTooltipController): void => {
	setContext(REWARD_TOOLTIP_KEY, controller);
};

export const getRewardTooltip = (): RewardTooltipController | undefined =>
	getContext<RewardTooltipController>(REWARD_TOOLTIP_KEY);
