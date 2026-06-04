import { getContext, setContext } from 'svelte';
import type { ResolvedReward } from './challenges-view.svelte';

/**
 * Cross-cutting controller for the single reward tooltip, registered once by the
 * Challenges screen and consumed by every (deeply nested) reward affordance via
 * context — so we don't thread hover handlers through the rail/overview/detail
 * component tree. Mirrors how the inventory grid drives the shared tooltip, but
 * shared through context rather than props.
 */
export interface RewardTooltipController {
	show: (reward: ResolvedReward, ev: MouseEvent) => void;
	move: (ev: MouseEvent) => void;
	hide: () => void;
}

const REWARD_TOOLTIP_KEY = Symbol('reward-tooltip');

export const setRewardTooltip = (controller: RewardTooltipController): void => {
	setContext(REWARD_TOOLTIP_KEY, controller);
};

export const getRewardTooltip = (): RewardTooltipController | undefined =>
	getContext<RewardTooltipController>(REWARD_TOOLTIP_KEY);
