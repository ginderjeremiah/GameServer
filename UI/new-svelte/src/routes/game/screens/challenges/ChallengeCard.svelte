<div class="challenge-card" class:completed={isCompleted}>
	<div class="challenge-header">
		<span class="challenge-name">{challenge.name}</span>
		{#if isCompleted}
			<span class="completed-badge">Completed</span>
		{/if}
	</div>
	<p class="challenge-description">{challenge.description}</p>
	<div class="progress-bar-container">
		<div class="progress-bar" style="width: {progressPercent}%"></div>
	</div>
	<div class="progress-text">{progress}/{challenge.progressGoal}</div>
	{#if rewardName}
		<div class="reward">Reward: {rewardName}</div>
	{/if}
</div>

<script lang="ts">
import type { IChallenge, IPlayerChallenge } from '$lib/api';
import { staticData } from '$stores';

type Props = {
	challenge: IChallenge;
	playerChallenge?: IPlayerChallenge;
};

const { challenge, playerChallenge }: Props = $props();

const isCompleted = $derived(playerChallenge?.completed ?? false);
const progress = $derived(playerChallenge?.progress ?? 0);
const progressPercent = $derived(Math.min(100, (progress / Math.max(1, challenge.progressGoal)) * 100));

const rewardName = $derived.by(() => {
	if (challenge.rewardItemId != null) {
		return staticData.items[challenge.rewardItemId]?.name ?? 'Unknown Item';
	}
	if (challenge.rewardItemModId != null) {
		return staticData.itemMods[challenge.rewardItemModId]?.name ?? 'Unknown Modifier';
	}
	return undefined;
});
</script>

<style lang="scss">
.challenge-card {
	border: var(--default-border);
	border-radius: 0.5rem;
	padding: 0.75rem;
	background-color: var(--slot-background-color);
	margin-bottom: 0.5rem;

	&.completed {
		opacity: 0.7;
		border-color: green;
	}

	.challenge-header {
		display: flex;
		justify-content: space-between;
		align-items: center;
		margin-bottom: 0.25rem;

		.challenge-name {
			font-weight: bold;
			font-size: 0.9rem;
		}

		.completed-badge {
			color: green;
			font-size: 0.75rem;
			font-weight: bold;
		}
	}

	.challenge-description {
		font-size: 0.8rem;
		margin: 0.25rem 0;
		color: var(--text-color);
		opacity: 0.8;
	}

	.progress-bar-container {
		height: 0.5rem;
		background-color: rgba(255, 255, 255, 0.1);
		border-radius: 0.25rem;
		overflow: hidden;
		margin: 0.25rem 0;

		.progress-bar {
			height: 100%;
			background-color: #4caf50;
			border-radius: 0.25rem;
			transition: width 0.3s ease;
		}
	}

	.progress-text {
		font-size: 0.75rem;
		text-align: right;
		color: var(--text-color);
		opacity: 0.6;
	}

	.reward {
		font-size: 0.8rem;
		margin-top: 0.25rem;
		color: gold;
	}
}
</style>
