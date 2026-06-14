<!-- Registers a reward-tooltip controller (the Challenges screen's role) so a
     RewardAffordance can be tested in isolation for its focus/hover wiring. -->
<RewardAffordance {reward} {variant} />

<script lang="ts">
import RewardAffordance from '$routes/game/screens/challenges/RewardAffordance.svelte';
import { setRewardTooltip, type RewardTooltipController } from '$routes/game/screens/challenges/reward-tooltip-context';
import type { ResolvedReward } from '$routes/game/screens/challenges/challenges-view.svelte';

interface Props {
	reward: ResolvedReward | null;
	variant?: 'tile' | 'chip';
	controller: RewardTooltipController;
}

const { reward, variant = 'tile', controller }: Props = $props();

// Context is registered once on mount (as the real Challenges screen does); the
// test controller is a fixed object, so capturing its initial value is intended.
// svelte-ignore state_referenced_locally
setRewardTooltip(controller);
</script>
