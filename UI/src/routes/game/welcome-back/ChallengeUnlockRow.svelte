<!-- One completed-challenge row in the welcome-back summary: the challenge name plus the reward it
	unlocked (rendered in the reward's rarity/skill accent). Presentational. -->
<li class="unlock-row">
	<span class="challenge-name">{completed.name}</span>
	{#if completed.reward}
		<span class="reward" style:color={completed.reward.accent} title={completed.reward.sub}>
			{completed.reward.name}
		</span>
	{:else}
		<span class="reward none">Completed</span>
	{/if}
</li>

<script lang="ts">
import type { CompletedChallengeView } from './offline-summary';

interface Props {
	completed: CompletedChallengeView;
}

let { completed }: Props = $props();
</script>

<style lang="scss">
.unlock-row {
	display: flex;
	align-items: baseline;
	justify-content: space-between;
	gap: 12px;
	padding: 7px 0;
	border-bottom: 1px solid var(--border-subtle);

	&:last-child {
		border-bottom: none;
	}
}

.challenge-name {
	color: var(--text-secondary);
	font-size: 0.85rem;
	min-width: 0;
	overflow: hidden;
	text-overflow: ellipsis;
	white-space: nowrap;
}

.reward {
	font-size: 0.85rem;
	font-weight: 600;
	flex-shrink: 0;

	&.none {
		color: var(--text-muted);
		font-weight: 400;
	}
}
</style>
