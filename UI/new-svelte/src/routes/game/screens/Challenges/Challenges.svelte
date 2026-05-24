<div class="challenges-screen">
	<h2 class="challenges-title">Challenges</h2>
	{#if loading}
		<p class="loading-text">Loading challenges...</p>
	{:else}
		<div class="challenges-list">
			{#each allChallenges as challenge}
				<ChallengeCard
					{challenge}
					playerChallenge={playerProgressMap.get(challenge.id)}
				/>
			{/each}
			{#if allChallenges.length === 0}
				<p class="empty-text">No challenges available.</p>
			{/if}
		</div>
	{/if}
</div>

<script lang="ts">
import { ApiRequest, type IPlayerChallenge } from '$lib/api';
import { staticData } from '$stores';
import ChallengeCard from './ChallengeCard.svelte';
import { onMount } from 'svelte';

let playerChallenges = $state<IPlayerChallenge[]>([]);
let loading = $state(true);

const allChallenges = $derived(staticData.challenges ?? []);
const playerProgressMap = $derived(
	new Map(playerChallenges.map((pc) => [pc.challengeId, pc]))
);

onMount(async () => {
	try {
		playerChallenges = await ApiRequest.get('Challenges/Player') ?? [];
	} catch {
		playerChallenges = [];
	}
	loading = false;
});
</script>

<style lang="scss">
.challenges-screen {
	padding: 1rem;
	max-width: 40rem;
	margin: 0 auto;

	.challenges-title {
		text-align: center;
		color: var(--title-color);
		margin-bottom: 1rem;
	}

	.loading-text,
	.empty-text {
		text-align: center;
		color: var(--text-color);
		opacity: 0.6;
	}

	.challenges-list {
		max-height: 70vh;
		overflow-y: auto;
		padding-right: 0.5rem;
	}
}
</style>
